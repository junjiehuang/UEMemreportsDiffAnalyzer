
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Data;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Formula.Functions;
using Spire.Xls;
using Spire.Xls.Core;
using NPOI.XSSF.Streaming.Values;
using SixLabors.Fonts;
using ICSharpCode.SharpZipLib.Tar;
using Core;
using System.Diagnostics.Tracing;
using static NPOI.HSSF.Util.HSSFColor;
using System.Windows.Input;
using NPOI.HPSF;
using System.Reflection;
using Org.BouncyCastle.Bcpg;

namespace MemReportParser
{
    public enum ParseState
    {
        SEARCHING = 0,
        BRIEF_ANDROID_MEMINFO = 10,
        BRIEF_UE_MEMORY_INFO = 20,
        PLATFORM_MEM_STATS = 100,
        MALLOC_BINNED2_MEM = 200,
        BINNED_ALLOCATOR_STATS = 300,
        POOL_STATS = 400,
        MEMORY_STATS = 500,
        RHI_STATS = 600,
        RHI_RENDER_TARGET2D_INFO = 650,
        RHI_RENDER_TARGET3D_INFO = 651,
        RHI_RENDER_TARGETCUBE_INFO = 652,
        RHI_TEXTURE2D_INFO = 653,
        RHI_TEXTURE3D_INFO = 654,
        RHI_TEXTURECUBE_INFO = 655,
        POOLED_RENDER_TARGETS = 700,
        TEXTURE_LIST = 800,
        OBJECT_CLASS_LIST = 900,
        OBJECT_LIST = 1000,
        PERSISTENT_LEVEL_SPAWNED_ACTORS = 1100,
        LUA_MEMORY = 1200,   
        WWISE_MEMORY = 1300,
        TEXTURE_DISTRIBUTION = 1400,
        TEXTURE_DISTRIBUTION_RenderTarget2D = 1401,
        TEXTURE_DISTRIBUTION_RenderTarget3D = 1402,
        TEXTURE_DISTRIBUTION_RenderTargetCube = 1403,
        TEXTURE_DISTRIBUTION_Texture2D = 1404,
        TEXTURE_DISTRIBUTION_Texture3D = 1405,
        TEXTURE_DISTRIBUTION_TextureCube = 1406,
    }

    // 一个数据单元
    public class Entry
    {
        public string MemReportFileName { get; private set; }
        public float Value { get; set; }

        public Entry(string memReportFileName, float value)
        {
            MemReportFileName = memReportFileName;
            Value = value;
        }
    }

    // 一列信息，字符串类型
    public class ColumeExtraInfo
    {
        public string ColumeValue;
        public string MemReportFileName;

        public ColumeExtraInfo(string memReportFileName, string value)
        {
            MemReportFileName = memReportFileName;
            ColumeValue = value;
        }
    }

    // 一列对比数据
    public class ColumeEntry
    {
        public string ColumeName = "";
        public string ColumeTag = "";
        public List<Entry> Entries = new List<Entry>();
        public float Diff = 0;
        public List<ColumeExtraInfo> ExtraInfos = new List<ColumeExtraInfo>();

        public void Add(string MemReportFileName, float value)
        {
            foreach (var e in Entries)
            {
                // 对于重复的项，采用累加的形式记录
                if (e.MemReportFileName == MemReportFileName)
                {
                    e.Value += value;
                    return;
                }
            }
            Entries.Add(new Entry(MemReportFileName, value));
        }

        public Entry GetEntry(string MemReportFileName)
        {
            foreach (var e in Entries)
            {
                if (e.MemReportFileName == MemReportFileName)
                    return e;
            }

            return null;
        }

        public void CalcDiff()
        {
            if (Entries.Count == 1)
                Diff = Entries[0].Value;
            else if (Entries.Count > 1)
                Diff = Entries[Entries.Count - 1].Value - Entries[0].Value;
        }

        public void AddExtraInfo(string MemReportFileName, string value)
        {
            foreach (var e in ExtraInfos)
            {
                if (e.MemReportFileName == MemReportFileName)
                    return;
            }
            ExtraInfos.Add(new ColumeExtraInfo(MemReportFileName, value));
        }
    }

    // 一行对比数据
    public class RowEntry
    {
        public string RowName = "";
        public List<ColumeEntry> ColumeEntries = new List<ColumeEntry>();

        public void Add(string ColumeName, string MemReportFileName, float value, string tag = "")
        {
            ColumeEntry CE = null;
            foreach (var ce in ColumeEntries)
            {
                if (ce.ColumeName == ColumeName)
                {
                    CE = ce;
                    break;
                }
            }
            if (CE == null)
            {
                CE = new ColumeEntry();
                CE.ColumeName = ColumeName;
                CE.ColumeTag = tag;
                ColumeEntries.Add(CE);
            }
            CE.Add(MemReportFileName, value);
        }

        public void AddExtraColume(string ColumeName, string MemReportFileName, string value)
        {
            ColumeEntry CE = null;
            foreach (var ce in ColumeEntries)
            {
                if (ce.ColumeName == ColumeName)
                {
                    CE = ce;
                    break;
                }
            }
            if (CE == null)
            {
                CE = new ColumeEntry();
                CE.ColumeName = ColumeName;
                ColumeEntries.Add(CE);
            }
            CE.AddExtraInfo(MemReportFileName, value);
        }
    }

    // 一组对比数据
    public class GroupEntry
    {
        public string GroupName = "";
        public int GroupID = 0;
        public Dictionary<string, RowEntry> RowDatas = new Dictionary<string, RowEntry>();
        public bool NeedCalcTotal = false;

        public RowEntry GetRowEntry(string rowName)
        {
            RowEntry row = null;
            RowDatas.TryGetValue(rowName, out row);
            return row;
        }
    }

    public class EntryTreeNode
    {
        public string NodeDesc = "";
        public float MemSizeMB = 0.0f;
        public GroupEntry GroupData = null;
        public List<EntryTreeNode> SubNodes = new List<EntryTreeNode>();
    }

    // 管理所有组的对比数据
    public class DataManager
    {
        public static bool EXPORT_AS_MULTISHEETS_EXCEL = true;

        public Dictionary<string, GroupEntry> AllDatas = new Dictionary<string, GroupEntry>();

        public List<EntryTreeNode> DataTree = new List<EntryTreeNode>();

        public GroupEntry GetGroupEntry(string key)
        {
            GroupEntry ge = null;
            AllDatas.TryGetValue(key, out ge);
            return ge;
        }

        public void SetGroupNeedCalcTotal(string groupName)
        {
            GroupEntry ge = GetGroupEntry(groupName);
            if(ge!=null)
            {
                ge.NeedCalcTotal = true;
            }
        }

        public RowEntry GetRowEntry(int GroupID, string GroupName, string RowName)
        {
            GroupEntry groupEntry = null;
            if (AllDatas.ContainsKey(GroupName) == false)
            {
                groupEntry = new GroupEntry();
                groupEntry.GroupName = GroupName;
                groupEntry.GroupID = GroupID;
                AllDatas.Add(GroupName, groupEntry);
            }
            else
            {
                groupEntry = AllDatas[GroupName];
            }

            if (groupEntry.RowDatas.ContainsKey(RowName))
            {
                return groupEntry.RowDatas[RowName];
            }
            else
            {
                RowEntry re = new RowEntry();
                re.RowName = RowName;
                groupEntry.RowDatas.Add(RowName, re);
                return re;
            }
        }

        static string[] SplitAndTrim(string line, char splitChar)
        {
            string[] words = line.Split(splitChar);
            List<string> trimmedWords = new List<string>();
            foreach (string word in words)
            {
                if (string.IsNullOrEmpty(word) || string.IsNullOrWhiteSpace(word))
                {
                }
                else
                {
                    trimmedWords.Add(word.Trim());
                }
            }
            return trimmedWords.ToArray();
        }

        static string[] SplitAndTrim(string line, string splitStr)
        {
            string[] words = line.Split(splitStr);
            List<string> trimmedWords = new List<string>();
            foreach (string word in words)
            {
                if (string.IsNullOrEmpty(word) || string.IsNullOrWhiteSpace(word))
                {
                }
                else
                {
                    trimmedWords.Add(word.Trim());
                }
            }
            return trimmedWords.ToArray();
        }

        static bool ParseInMemOnDisk(string line, out float inMemSize, out float onDiskSize)
        {
            int a1 = line.IndexOf("InMem= ");
            int b1 = line.IndexOf("OnDisk= ");
            if (a1 != -1 && b1 != -1)
            {
                int a2 = -1;
                int b2 = -1;
                // UE4:
                // Total size: InMem = 283.43 MB OnDisk = 355.34 MB Count = 979
                if (line.Contains("KB"))
                {
                    a2 = line.IndexOf(" KB", a1);
                    b2 = line.IndexOf(" KB", b1);
                }
                // UE5:
                // Total size: InMem= 152.16 MB  OnDisk= 343.17 MB  Count=218, CountApplicableToMin=89
                else if (line.Contains("MB"))
                {
                    a2 = line.IndexOf(" MB", a1);
                    b2 = line.IndexOf(" MB", b1);
                }
                if (a2 != -1 && b2 != -1)
                {
                    a1 += 7;
                    b1 += 8;
                    string inMemStr = line.Substring(a1, a2 - a1).Trim();
                    string onDisStr = line.Substring(b1, b2 - b1).Trim();
                    float inMemMB = 0.0f;
                    float onDiskMB = 0.0f;
                    if (float.TryParse(inMemStr, out inMemMB) && float.TryParse(onDisStr, out onDiskMB))
                    {
                        inMemSize = inMemMB;//(long)(inMemMB * 1024.0f * 1024.0f);
                        onDiskSize = onDiskMB;//(long)(onDiskMB * 1024.0f * 1024.0f);
                        return true;
                    }
                }
            }
            inMemSize = 0;
            onDiskSize = 0;
            return false;
        }

        void ParseBinnedMemory2(string line, string preText, string postTextA, string postTextB, string fileName)
        {
            int PART_ID = 2;
            if (line.Contains("Current Memory") || line.Contains("Peak Memory"))
                PART_ID = 1;
            else if (line.Contains("Current Slack"))
                PART_ID = 3;
            string GroupName = "Allocator Stats for binned - Part" + PART_ID;
            int GroupID = (int)ParseState.BINNED_ALLOCATOR_STATS + PART_ID;
            string[] words = line.Replace(preText, "").Split(' ');
            float memUsed = 0.0f;
            float memWaste = 0.0f;
            if (words.Length > 4)
            {
                int index = PART_ID == 1 ? 4 : 3;
                if (float.TryParse(words[index], out memWaste))
                {
                    string ColName = PART_ID == 1 ? "MemWaste" : "MemPeak";
                    GetRowEntry(GroupID, GroupName, preText).Add(ColName, fileName, memWaste, "MB");
                }
            }
            if (words.Length > 0)
            {
                if (float.TryParse(words[0], out memUsed))
                {
                    string ColName = PART_ID == 1 ? "MemUsed" : "MemCurrent";
                    GetRowEntry(GroupID, GroupName, preText).Add(ColName, fileName, memUsed, "MB");
                }
            }
        }

        public void GenDataFromFiles(ref List<string> FileNameLst, ref List<string> FileFullPathLst)
        {
            Dictionary<string, int> ObjGroupIDMap = new Dictionary<string, int>();
            foreach (string file in FileFullPathLst)
            {
                string className = "";
                string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                string[] lines = System.IO.File.ReadAllLines(file);
                ParseState state = ParseState.SEARCHING;
                string texDistCat = "";
                ParseState catState = ParseState.TEXTURE_DISTRIBUTION;

                foreach (string line in lines)
                {
                    //Obj List:
                    //Obj List: class=SoundWave
                    //Obj List: class=SkeletalMesh
                    //Obj List: class=StaticMesh
                    //Obj List: class=Level
                    if (line.Contains("Obj List:"))
                    {
                        className = "";
                        if (line.Contains("class="))
                        {
                            className = line.Replace("-alphasort", "").Trim().Replace("Obj List: class=", "").Trim().Replace("-resourcesizesort", "").Trim();
                        }
                        switch (className)
                        {
                            default:
                                if (string.IsNullOrEmpty(className))
                                {
                                    state = ParseState.OBJECT_CLASS_LIST;
                                }
                                else
                                {
                                    state = ParseState.OBJECT_LIST;
                                }
                                //activeobjectStats = GetStatDict(allStats, "Object Classes");
                                break;

                            case "SoundWave":
                                state = ParseState.OBJECT_LIST;
                                //activeobjectStats = GetStatDict(allStats, "Object Soundwaves");
                                break;
                            case "SkeletalMesh":
                                state = ParseState.OBJECT_LIST;
                                //activeobjectStats = GetStatDict(allStats, "Object SkeletalMesh");
                                break;
                            case "StaticMesh":
                                state = ParseState.OBJECT_LIST;
                                //activeobjectStats = GetStatDict(allStats, "Object StaticMesh");
                                break;
                            case "Level":
                                state = ParseState.OBJECT_LIST;
                                //activeobjectStats = GetStatDict(allStats, "Object Level");
                                break;
                        }
                    }
                    else if (line.Contains("persistent level:"))
                    {
                        state = ParseState.PERSISTENT_LEVEL_SPAWNED_ACTORS;
                    }
                    else if (string.Compare(line, "Memory Stats:", true) == 0)
                    {
                        state = ParseState.MEMORY_STATS;
                    }
                    else if (line.Contains("RHI resource memory"))
                    {
                        state = ParseState.RHI_STATS;
                    }
                    else if (line.Contains("Allocator Stats for binned:"))
                    {
                        state = ParseState.BINNED_ALLOCATOR_STATS;
                    }
                    else if (line.Contains("Block Size  Num Pools"))
                    {
                        state = ParseState.POOL_STATS;
                    }
                    else if (line.Contains("Pooled Render Targets:"))
                    {
                        state = ParseState.POOLED_RENDER_TARGETS;
                    }
                    else if (line.Contains("Listing all textures."))
                    {
                        state = ParseState.TEXTURE_LIST;
                    }
                    else if (line.Contains("Platform Memory Stats for"))
                    {
                        state = ParseState.PLATFORM_MEM_STATS;
                    }
                    else if (line.Contains("FMallocBinned2 Mem"))
                    {
                        state = ParseState.MALLOC_BINNED2_MEM;
                    }
                    else if (line.Contains("Lua memory usage"))
                    {
                        state = ParseState.LUA_MEMORY;
                    }
                    else if (line.Contains("Android meminfo"))
                    {
                        state = ParseState.BRIEF_ANDROID_MEMINFO;
                    }
                    else if (line.Contains("RHIRenderTarget2D Count="))
                    {
                        state = ParseState.RHI_RENDER_TARGET2D_INFO;
                    }
                    else if (line.Contains("RHIRenderTarget3D Count="))
                    {
                        state = ParseState.RHI_RENDER_TARGET3D_INFO;
                    }
                    else if (line.Contains("RHIRenderTargetCube Count="))
                    {
                        state = ParseState.RHI_RENDER_TARGETCUBE_INFO;
                    }
                    else if (line.Contains("RHITexture2D Count="))
                    {
                        state = ParseState.RHI_TEXTURE2D_INFO;
                    }
                    else if (line.Contains("RHITexture3D Count="))
                    {
                        state = ParseState.RHI_TEXTURE3D_INFO;
                    }
                    else if (line.Contains("RHITextureCube Count="))
                    {
                        state = ParseState.RHI_TEXTURECUBE_INFO;
                    }
                    else if (line.Contains("WWISE "))
                    {
                        state = ParseState.WWISE_MEMORY;
                    }
                    else if (line.Contains("Texture Memory Distribution Brief Start"))
                    {
                        state = ParseState.TEXTURE_DISTRIBUTION;
                    }

                    switch (state)
                    {
                        case ParseState.SEARCHING:
                            break;

                        case ParseState.TEXTURE_LIST:
                            {
                                // Total size: InMem= 376.37 MB  OnDisk= 482.68 MB  Count=295, CountApplicableToMin=58
                                if (line.Contains("Total size: InMem"))
                                {
                                    //float inMemSize = 0;
                                    //float onDiskSize = 0;
                                    //if (ParseInMemOnDisk(line, out inMemSize, out onDiskSize))
                                    //{
                                    //    GetRowEntry(((int)ParseState.TEXTURE_LIST + 1), "Texture Total", "In Memory").Add("Size", fileName, inMemSize, "MB");
                                    //    GetRowEntry(((int)ParseState.TEXTURE_LIST + 1), "Texture Total", "On Disk").Add("Size", fileName, onDiskSize, "MB");
                                    //    GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", "Texture Total").Add("Size", fileName, inMemSize, "MB");
                                    //}
                                }
                                else if (line.Contains("Total PF_") || line.Contains("Total TEXTUREGROUP_"))
                                {
                                    //Total PF_B8G8R8A8 size: InMem = 180.20 MB OnDisk = 235.82 MB
                                    //Total PF_DXT1 size: InMem = 6.76 MB OnDisk = 7.87 MB
                                    //Total PF_DXT5 size: InMem = 63.54 MB OnDisk = 68.05 MB
                                    //Total PF_FloatRGBA size: InMem = 32.67 MB OnDisk = 43.33 MB
                                    //Total PF_BC5 size: InMem = 0.26 MB OnDisk = 0.26 MB
                                    //Total TEXTUREGROUP_World size: InMem = 71.65 MB OnDisk = 91.71 MB
                                    //Total TEXTUREGROUP_WorldNormalMap size: InMem = 0.30 MB OnDisk = 0.30 MB
                                    //Total TEXTUREGROUP_Vehicle size: InMem = 54.22 MB OnDisk = 55.56 MB
                                    //Total TEXTUREGROUP_Skybox size: InMem = 0.04 MB OnDisk = 0.04 MB
                                    //Total TEXTUREGROUP_UI size: InMem = 151.08 MB OnDisk = 200.17 MB
                                    //Total TEXTUREGROUP_Lightmap size: InMem = 0.48 MB OnDisk = 0.48 MB
                                    //Total TEXTUREGROUP_Shadowmap size: InMem = 0.33 MB OnDisk = 0.33 MB
                                    //Total TEXTUREGROUP_ColorLookupTable size: InMem = 4.28 MB OnDisk = 5.71 MB
                                    //Total TEXTUREGROUP_Bokeh size: InMem = 0.36 MB OnDisk = 0.36 MB
                                    //Total TEXTUREGROUP_Pixels2D size: InMem = 0.67 MB OnDisk = 0.67 MB
                                    float inMemSize = 0;
                                    float onDiskSize = 0;
                                    if (ParseInMemOnDisk(line, out inMemSize, out onDiskSize))
                                    {
                                        int e = line.IndexOf(" size: ");
                                        if (e != -1)
                                        {
                                            string key = line.Substring(0, e).Replace("Total ", "").Trim();
                                            if (line.Contains("Total TEXTUREGROUP_"))
                                            {
                                                GetRowEntry(((int)ParseState.TEXTURE_LIST + 3), "TextureGroupInMem", key).Add("Size", fileName, inMemSize, "MB");
                                                //GetRowEntry(((int)ParseState.TEXTURE_LIST + 4), "TextureGroup On Disk", key).Add("OnDiskSize", fileName, onDiskSize, "MB");
                                            }
                                            if (line.Contains("Total PF_"))
                                            {
                                                GetRowEntry(((int)ParseState.TEXTURE_LIST + 5), "TextureFormatInMem", key).Add("Size", fileName, inMemSize, "MB");
                                                //GetRowEntry(((int)ParseState.TEXTURE_LIST + 6), "TextureFormat On Disk", key).Add("OnDiskSize", fileName, onDiskSize, "MB");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    string[] words = SplitAndTrim(line, ',');
                                    // UE5:
                                    // MaxAllowedSize: Width x Height (Size in KB, Authored Bias), Current/InMem: Width x Height (Size in KB), Format, LODGroup, Name, Streaming, UnknownRef, VT, Usage Count, NumMips, Uncompressed
                                    // 0x0 (0 KB, ?), 0x0 (0 KB), PF_Unknown, TEXTUREGROUP_World, /CommonUI/DefaultMediaTexture.DefaultMediaTexture, NO, NO, NO, 0, 0, NO
                                    if (words.Length == 12)
                                    {
                                        // 0 - [MaxAllowedSize: Width x Height (Size in KB,]    : 2048x2048 (32768 KB
                                        // 1 - [Authored Bias)]                                 : 0)
                                        // 2 : [Current/InMem: Width x Height (Size in KB)]     : 2048x2048 (32768 KB)
                                        // 3 : [Format]                                         : PF_FloatRGBA
                                        // 4 : [LODGroup]                                       : TEXTUREGROUP_World
                                        // 5 : [Name]                                           : /Engine/EngineMaterials/DefaultBloomKernel.DefaultBloomKernel
                                        // 6 : [Streaming]                                      : NO
                                        // 7 : [UnknownRef]                                     : NO
                                        // 8 : [VT]                                             : NO
                                        // 9 : [Usage Count]                                    : 0
                                        //10 : [NumMips]                                        : 1
                                        //11 : [Uncompressed]                                   : YES
                                        int NumMips = 1;
                                        if (int.TryParse(words[10], out NumMips))
                                        {
                                            string key = words[5];
                                            long inMemSizeKB = 0;
                                            string[] inMemSize = SplitAndTrim(words[2], ' ');
                                            if (inMemSize.Length == 3)
                                            {
                                                int Streaming = words[6].Equals("YES") ? 1 : 0;
                                                int Uncompressed = words[11].Equals("YES") ? 1 : 0;
                                                string sizeKB = inMemSize[1].Replace("(", "").Trim();
                                                if (long.TryParse(sizeKB, out inMemSizeKB))
                                                {
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("Width x Height", fileName, inMemSize[0]);
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("LODGroup", fileName, words[4]);
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("Streaming", fileName, Streaming.ToString());
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("Uncompressed", fileName, Uncompressed.ToString());
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).Add("NumMips", fileName, int.Parse(words[10]), "");
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).Add("Size", fileName, inMemSizeKB, "KB");
                                                    //AddEntry(GetStatDict(allStats, "Texture In Mem"), fileName, key, inMemSizeKB, "KB");
                                                }
                                            }
                                        }
                                    }
                                    // UE4.18 : 
                                    //Cooked/OnDisk: Width x Height (Size in KB), Current/InMem: Width x Height (Size in KB), Format, LODGroup, Name, Streaming, Usage Count
                                    //32x32 (0 KB, ?), 32x32 (0 KB), PF_ASTC_8x8, TEXTUREGROUP_World, /Engine/EngineResources/Black.Black, NO, 0
                                    else if (words.Length == 7)
                                    {
                                        {
                                            // 0: "256x256 (43688 KB)"
                                            // 1: "2048x2048 (32768 KB)"
                                            // 2: "PF_FloatRGBA"
                                            // 3: "TEXTUREGROUP_World"
                                            // 4: "/Engine/EngineMaterials/DefaultBloomKernel.DefaultBloomKernel"
                                            // 5: "NO"
                                            // 6: 0
                                            int inMemory = 0;
                                            if (int.TryParse(words[6], out inMemory))
                                            {
                                                string key = words[4];
                                                // if (inMemory != 0)
                                                {
                                                    long inMemSizeKB = 0;
                                                    string[] inMemSize = SplitAndTrim(words[1], ' ');
                                                    if (inMemSize.Length == 3)
                                                    {
                                                        int Streaming = words[5].Equals("YES") ? 1 : 0;
                                                        string sizeKB = inMemSize[1].Replace("(", "").Trim();
                                                        if (long.TryParse(sizeKB, out inMemSizeKB))
                                                        {
                                                            GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("Width x Height", fileName, inMemSize[0]);
                                                            GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("LODGroup", fileName, words[4]);
                                                            GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("Streaming", fileName, Streaming.ToString());
                                                            GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).Add("Size", fileName, inMemSizeKB, "KB");
                                                        }
                                                    }
                                                }
                                                // else
                                                // {
                                                //     long onDiskSizeKB = 0;
                                                //     string[] inMemSize = SplitAndTrim(words[1], ' ');
                                                //     if (inMemSize.Length == 3)
                                                //     {
                                                //         int Streaming = words[5].Equals("YES") ? 1 : 0;
                                                //         string sizeKB = inMemSize[1].Replace("(", "").Trim();
                                                //         if (long.TryParse(sizeKB, out onDiskSizeKB))
                                                //         {
                                                //             GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture On Disk", key).AddExtraColume("Width x Height", fileName, inMemSize[0]);
                                                //             GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture On Disk", key).AddExtraColume("LODGroup", fileName, words[4]);
                                                //             GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture On Disk", key).AddExtraColume("Streaming", fileName, Streaming.ToString());
                                                //             GetRowEntry(((int)ParseState.TEXTURE_LIST + 8), "Texture On Disk", key).Add("SizeOnDisk", fileName, onDiskSizeKB, "KB");
                                                //         }
                                                //     }
                                                // }
                                            }
                                        }
                                    }
                                    // UE4.24 : 
                                    //Cooked/OnDisk: Width x Height (Size in KB, Authored Bias), Current/InMem: Width x Height (Size in KB), Format, LODGroup, Name, Streaming, Usage Count
                                    //128x256 (20 KB, ?), 32x64 (1 KB), PF_ASTC_6x6, TEXTUREGROUP_Effects, /Game/PaperMan/Effects/Textures/wb_1/T_DQ_98.T_DQ_98, YES, 0
                                    else if (words.Length == 8)
                                    {
                                        int UsageCount = 0;
                                        int Streaming = words[6].Equals("YES") ? 1 : 0;
                                        if (int.TryParse(words[7], out UsageCount))
                                        {
                                            long inMemSizeKB = 0;
                                            string[] inMemSize = SplitAndTrim(words[2], ' ');
                                            if (inMemSize.Length == 3)
                                            {
                                                string key = words[5];//string.Format("[{0}]_[{1}_[{2}]", inMemSize[0], words[4], words[5]);
                                                string sizeKB = inMemSize[1].Replace("(", "").Trim();
                                                if (long.TryParse(sizeKB, out inMemSizeKB))
                                                {
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("Width x Height", fileName, inMemSize[0]);
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("LODGroup", fileName, words[4]);
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("Streaming", fileName, Streaming.ToString());
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).Add("Size", fileName, inMemSizeKB, "KB");
                                                    //AddEntry(GetStatDict(allStats, "Texture In Mem"), fileName, key, inMemSizeKB, "KB");
                                                }
                                            }
                                        }
                                    }
                                    // Modify in klbq
                                    // Cooked/OnDisk: Width x Height (Size in KB, Authored Bias), Current/InMem: Width x Height (Size in KB), Format, LODGroup, Name, Streaming, Usage Count, VT, NumMips, Uncompressed
                                    else if(words.Length == 11)
                                    {
                                        string Streaming = words[6];
                                        int UsageCount = 0;
                                        string VT = words[8];
                                        int NumMips = 0;
                                        string Uncompressed = words[10];
                                        if (int.TryParse(words[7], out UsageCount)
                                            && int.TryParse(words[9], out NumMips))
                                        {
                                            long inMemSizeKB = 0;
                                            string[] inMemSize = SplitAndTrim(words[2], ' ');
                                            if (inMemSize.Length == 3)
                                            {
                                                string key = words[5];//string.Format("[{0}]_[{1}_[{2}]", inMemSize[0], words[4], words[5]);
                                                string sizeKB = inMemSize[1].Replace("(", "").Trim();
                                                if (long.TryParse(sizeKB, out inMemSizeKB))
                                                {
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("Width x Height", fileName, inMemSize[0]);
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("LODGroup", fileName, words[4]);
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("Streaming", fileName, Streaming);
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("UsageCount", fileName, UsageCount.ToString());
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("VirtualTexture", fileName, VT);
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("NumMips", fileName, NumMips.ToString());
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).AddExtraColume("Uncompressed", fileName, Uncompressed);
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "TextureInMem", key).Add("Size", fileName, inMemSizeKB, "KB");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            break;

                        case ParseState.POOLED_RENDER_TARGETS:
                            {
                                if (line.Length > 41 && line.Contains("mip(s)"))
                                {
                                    // UE4:
                                    //      0.250MB  256x 256           1mip(s) HZBResultsCPU (B8G8R8A8)
                                    // UE5:
                                    //      0.062MB    1x   1           1mip(s) Dummy (PF_R16F) Unused frames: 1
                                    string sizeMB = line.Substring(0, 10).Trim().Replace("MB", "");
                                    string sizeRes = line.Substring(11, 18).Trim();
                                    string numMips = line.Substring(30, 9).Trim().Replace("mip(s)", "");
                                    string name = line.Substring(39).Trim();
                                    string key = name;//string.Format("{0}-{1}-{2}", name, sizeRes, numMips);
                                    float valueMB = 0.0f;
                                    if (float.TryParse(sizeMB, out valueMB))
                                    {
                                        GetRowEntry((int)ParseState.POOLED_RENDER_TARGETS, "Render Target Pools", key).AddExtraColume("Mip(s)", fileName, numMips);
                                        GetRowEntry((int)ParseState.POOLED_RENDER_TARGETS, "Render Target Pools", key).AddExtraColume("Resolution", fileName, sizeRes);
                                        GetRowEntry((int)ParseState.POOLED_RENDER_TARGETS, "Render Target Pools", key).Add("SizeMem", fileName, valueMB, "MB");
                                    }
                                }
                            }
                            break;

                        case ParseState.POOL_STATS:
                            {
                                string[] words = SplitAndTrim(line, ' ');
                                if (words.Length == 11)
                                {
                                    string RowKey = string.Format("BlockSize_{0}", words[0]);
                                    {
                                        long value = 0;
                                        if (long.TryParse(words[3], out value))
                                        {
                                            GetRowEntry((int)ParseState.POOL_STATS, "MemPool Allocs", RowKey).Add("CurAllocs", fileName, value, "Count");
                                        }
                                    }
                                    {
                                        long value = 0;
                                        if (long.TryParse(words[2], out value))
                                        {
                                            GetRowEntry((int)ParseState.POOL_STATS, "MemPool Allocs", RowKey).Add("NumPools", fileName, value, "Count");
                                        }
                                    }
                                    {
                                        long value = 0;
                                        if (long.TryParse(words[8].Replace("K", ""), out value))
                                        {
                                            GetRowEntry((int)ParseState.POOL_STATS, "MemPool Allocs", RowKey).Add("MemSlack", fileName, value, "KB");
                                        }
                                    }
                                    {
                                        long value = 0;
                                        if (long.TryParse(words[7].Replace("K", ""), out value))
                                        {
                                            GetRowEntry((int)ParseState.POOL_STATS, "MemPool Allocs", RowKey).Add("MemUsed", fileName, value, "KB");
                                        }
                                    }
                                }
                            }
                            break;

                        case ParseState.BINNED_ALLOCATOR_STATS:

                        case ParseState.OBJECT_CLASS_LIST:
                            {
                                {
                                    string strGroupName = "Obj Classes";
                                    // UE4:
                                    // Class    Count      NumKB      MaxKB   ResExcKB  ResExcDedSysKB  ResExcShrSysKB  ResExcDedVidKB  ResExcShrVidKB     ResExcUnkKB
                                    string[] words = SplitAndTrim(line, ' ');
                                    if (words.Length == 10)
                                    {
                                        long count;
                                        if (long.TryParse(words[1], out count))
                                        {
                                            string key = words[0];
                                            GetRowEntry((int)ParseState.OBJECT_CLASS_LIST, strGroupName, key).Add("Count", fileName, count, "Count");
                                        }

                                        int finds = 0;
                                        float NumKB;
                                        if (float.TryParse(words[2], out NumKB))
                                        {
                                            ++finds;
                                            string key = words[0];
                                            GetRowEntry((int)ParseState.OBJECT_CLASS_LIST, strGroupName, key).Add("NumKB", fileName, NumKB, "KB");
                                        }

                                        float ResExcKB;
                                        if (float.TryParse(words[4], out ResExcKB))
                                        {
                                            ++finds;
                                            string key = words[0];
                                            GetRowEntry((int)ParseState.OBJECT_CLASS_LIST, strGroupName, key).Add("ResExcKB", fileName, ResExcKB, "KB");
                                        }

                                        if(finds == 2)
                                        {
                                            string key = words[0];
                                            float accumMB = (NumKB + ResExcKB) / 1024f;
                                            GetRowEntry((int)ParseState.OBJECT_CLASS_LIST, strGroupName, key).Add("Compound", fileName, accumMB, "MB");
                                        }

                                        SetGroupNeedCalcTotal(strGroupName);
                                    }
                                    // UE5:
                                    // Class    Count      NumKB      MaxKB   ResExcKB  ResExcDedSysKB  ResExcDedVidKB     ResExcUnkKB
                                    else if (words.Length == 8)
                                    {
                                        long count;
                                        if (long.TryParse(words[1], out count))
                                        {
                                            string key = words[0];
                                            GetRowEntry((int)ParseState.OBJECT_CLASS_LIST, strGroupName, key).Add("Count", fileName, count, "Count");
                                        }

                                        float NumKB;
                                        if (float.TryParse(words[2], out NumKB))
                                        {
                                            string key = words[0];
                                            GetRowEntry((int)ParseState.OBJECT_CLASS_LIST, strGroupName, key).Add("NumKB", fileName, NumKB, "KB");
                                        }

                                        float ResExcKB;
                                        if (float.TryParse(words[4], out ResExcKB))
                                        {
                                            string key = words[0];
                                            GetRowEntry((int)ParseState.OBJECT_CLASS_LIST, strGroupName, key).Add("ResExcKB", fileName, ResExcKB, "KB");
                                        }

                                        SetGroupNeedCalcTotal(strGroupName);
                                    }
                                    // UE 4.24
                                    // 36111 Objects (Total: 34.881M / Max: 36.822M / Res: 221.289M | ResDedSys: 58.856M / ResShrSys: 0.000M / ResDedVid: 138.698M / ResShrVid: 0.000M / ResUnknown: 23.736M)
                                    else if (line.Contains("Objects (Total:"))
                                    {
                                        string[] segs = line.Split('/');

                                        words = segs[0].Split(' ');
                                        string strObjMB = words[3].Replace("M", "");
                                        float objMB;
                                        if(float.TryParse(strObjMB, out objMB))
                                        {
                                            string key = "Group [UObject] : UObjects Memory";
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, objMB, "MB");
                                        }

                                        words = segs[2].Split(' ');
                                        string strResMB = words[2].Replace("M", "");
                                        float resMB;
                                        if (float.TryParse(strResMB, out resMB))
                                        {
                                            string key = "Group [UObject] : UObject Reference Res Memory";
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, resMB, "MB");
                                        }
                                    }
                                }
                            }
                            break;

                        case ParseState.OBJECT_LIST:
                            {
                                {
                                    string srGroupName = "Object " + className;
                                    int GroupID;
                                    if (ObjGroupIDMap.TryGetValue(className, out GroupID) == false)
                                    {
                                        GroupID = (int)ParseState.OBJECT_LIST + ObjGroupIDMap.Count + 1;
                                        ObjGroupIDMap.Add(className, GroupID);
                                    }

                                    // UE4:
                                    // Object NumKB      MaxKB ResExcKB  ResExcDedSysKB ResExcShrSysKB  ResExcDedVidKB ResExcShrVidKB     ResExcUnkKB
                                    string[] words = SplitAndTrim(line, ' ');
                                    if (words.Length == 10)
                                    {
                                        long Count;
                                        if (long.TryParse(words[1], out Count) == false)
                                        {
											int finds = 0;
                                            float numKB;
                                            if (float.TryParse(words[2], out numKB))
                                            {
												++finds;
                                                string key = words[1];
                                                GetRowEntry(GroupID, srGroupName, key).Add("NumKB", fileName, numKB, "KB");
                                                SetGroupNeedCalcTotal(srGroupName);
                                            }
											
											float resKB;
											if (float.TryParse(words[4], out resKB))
											{
												++finds;
												string key = words[1];
												GetRowEntry(GroupID, srGroupName, key).Add("ResKB", fileName, resKB, "KB");
                                                SetGroupNeedCalcTotal(srGroupName);
											}
											
											if (finds == 2)
											{
												string key = words[1];
												GetRowEntry(GroupID, srGroupName, key).Add("Compound", fileName, (resKB+numKB)/1024.0f, "MB");
                                                SetGroupNeedCalcTotal(srGroupName);
											}
                                        }
                                    }
                                    // UE5:
                                    else if (words.Length == 8)
                                    {
                                        int count;
                                        //Class    Count      NumKB      MaxKB   ResExcKB  ResExcDedSysKB  ResExcDedVidKB     ResExcUnkKB
                                        //SkeletalMesh        5   61925.96   61927.10   15810.11          191.64            0.00        15618.47
                                        if (int.TryParse(words[1], out count) == false)
                                        {
                                            // Object      NumKB      MaxKB   ResExcKB  ResExcDedSysKB  ResExcDedVidKB     ResExcUnkKB
                                            //SkeletalMesh /Game/Resources/PolygonTown/Meshes/Characters/SK_Character_Son_01.SK_Character_Son_01     897.23     897.41     255.16           20.66            0.00          234.51
                                            {
												int finds = 0;
                                                float numKB;
                                                if (float.TryParse(words[2], out numKB))
                                                {
													++finds;
                                                    string key = words[1];
                                                    GetRowEntry(GroupID, srGroupName, key).Add("NumKB", fileName, numKB, "KB");
                                                    SetGroupNeedCalcTotal(srGroupName);
                                                }												
												
												float resKB;
												if (float.TryParse(words[4], out resKB))
												{
													++finds;
													string key = words[1];
													GetRowEntry(GroupID, srGroupName, key).Add("ResKB", fileName, resKB, "KB");
													SetGroupNeedCalcTotal(srGroupName);
												}
												
												if (finds == 2)
												{
													string key = words[1];
													GetRowEntry(GroupID, srGroupName, key).Add("Compound", fileName, (resKB+numKB)/1024.0f, "MB");
													SetGroupNeedCalcTotal(srGroupName);
												}
                                            }
                                        }
                                    }
                                }
                            }
                            break;

                        case ParseState.RHI_STATS:
                            {
                                string[] Sep = { "MB", };
                                string[] words = SplitAndTrim(line, '-');
                                if (words.Length >= 3)
                                {
                                    string key = words[2];
                                    float value;
                                    bool hasMB = words[0].Contains("MB");
                                    if (hasMB)
                                    {
                                        string[] datas = words[0].Split(Sep, StringSplitOptions.RemoveEmptyEntries);
                                        words[0] = datas[0];
                                    }
                                    if (float.TryParse(words[0], out value))
                                    {
                                        if (hasMB == false)
                                            value /= (1024 * 1024);
                                        GetRowEntry((int)ParseState.RHI_STATS, "RHI Memory", key).Add("SizeMem", fileName, value, "MB");
                                    }
                                }
                                //else if(line.Contains("total"))
                                //{
                                //    words = SplitAndTrim(line, ' ');
                                //    if(words.Length == 2)
                                //    {
                                //        string strmb = words[0].Replace("MB", "");
                                //        float mb;
                                //        if(float.TryParse(strmb, out mb))
                                //        {
                                //            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", "RHI Memory").Add("Size", fileName, mb, "MB");
                                //        }
                                //    }
                                //}
                                
                            }
                            break;

                        case ParseState.MEMORY_STATS:
                            {
                                if(line.Contains("-"))
                                {
                                    string[] words = SplitAndTrim(line, " - ");
                                    if (words.Length >= 3)
                                    {
                                        string key = words[2];
                                        string parKey = words[3];
                                        float value;
                                        bool hasMB = words[0].Contains("MB");
                                        if (hasMB)
                                            words[0] = words[0].Replace("MB", "");
                                        if (float.TryParse(words[0], out value))
                                        {
                                            if (hasMB == false)
                                                value /= (1024 * 1024);
                                            GetRowEntry((int)ParseState.MEMORY_STATS, "Stat Memory", key).AddExtraColume("StatGroup", fileName, parKey);
                                            GetRowEntry((int)ParseState.MEMORY_STATS, "Stat Memory", key).Add("SizeMem", fileName, value, "MB");
                                        }

                                        // unlua 容器分配的内存
                                        if (line.Contains("STATGROUP_UnLua"))
                                        {                                            
                                            GetRowEntry((int)ParseState.LUA_MEMORY, "Lua Memory", key).Add("Size", fileName, value, "MB");
                                            key = "Group [UnLua]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_AI_EQS"))
                                        {
                                            key = "Group [AI_EQS]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_Audio"))
                                        {
                                            key = "Group [Audio]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_GeometryCache"))
                                        {
                                            key = "Group [GeometryCache]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }                                        
                                        else if (line.Contains("STATGROUP_LLMOverhead") ||
                                            line.Contains("STATGROUP_StatSystem"))
                                        {
                                            key = "Group [Overhead]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_MapBuildData"))
                                        {
                                            key = "Group [MapBuildData]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_MemoryStaticMesh"))
                                        {
                                            key = "Group [MemoryStaticMesh]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_Navigation"))
                                        {
                                            key = "Group [Navigation]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_OpenGLRHI"))
                                        {
                                            key = "Group [RHI]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if(line.Contains("STATGROUP_ParametrixAI"))
                                        {
                                            key = "Group [ParametrixAI]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_ParticleMem"))
                                        {
                                            key = "Group [ParticleMem]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_RHI"))
                                        {
                                            key = "Group [RHI]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_SceneMemory"))
                                        {
                                            key = "Group [SceneMemory]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_Shaders"))
                                        {
                                            key = "Group [Shaders]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_ShadowRendering"))
                                        {
                                            key = "Group [ShadowRendering]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_SlateMemory"))
                                        {
                                            key = "Group [Slate]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                        else if (line.Contains("STATGROUP_PipelineStateCache"))
                                        {
                                            key = "Group [PipelineStateCache]: " + key;
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, value, "MB");
                                        }
                                    }
                                }
                                else if (line.Contains("FMemStack") || line.Contains("Nametable") || line.Contains("AssetRegistry"))
                                {
                                    string[] segs = SplitAndTrim(line, '=');
                                    string[] words = SplitAndTrim(segs[0], ' ');
                                    string key = words[0];
                                    float mb;
                                    words = SplitAndTrim(segs[1], ' ');
                                    if (float.TryParse(words[0], out mb))
                                    {
                                        key = "Group [" + key + "]";
                                        GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, mb, "MB");
                                    }
                                }
                                else if (line.Contains("FPageAllocator"))
                                {
                                    string[] segs = SplitAndTrim(line, '=');
                                    string[] words = SplitAndTrim(segs[0], ' ');
                                    string key = words[0];
                                    float mbUsed, mbUnused;
                                    string repstr = segs[1].Replace("[", "").Replace("]", "").Replace(" /", "");
                                    words = SplitAndTrim(repstr, ' ');
                                    if (float.TryParse(words[0], out mbUsed) && float.TryParse(words[1], out mbUnused))
                                    {
                                        key = "Group [" + key + "]";
                                        GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, (mbUsed + mbUnused), "MB");
                                    }
                                }
                            }
                            break;

                        case ParseState.PERSISTENT_LEVEL_SPAWNED_ACTORS:
                            {
                                string[] words = SplitAndTrim(line, ',');
                                if (words.Length == 6)
                                {
                                    string key = words[3];
                                    if (key != "Class")
                                    {
                                        long value = 1;
                                        GetRowEntry((int)ParseState.PERSISTENT_LEVEL_SPAWNED_ACTORS, "PersistentLevelSpawnedActors", key)
                                            .Add("Count", fileName, value, "Count");
                                    }
                                }
                            }
                            break;

                        case ParseState.PLATFORM_MEM_STATS:
                            {
								// 先忽略掉这部分的数据导出
								continue;
								
                                // UE5:
                                // Platform Memory Stats for Android 
                                // Process Physical Memory: 1465.61 MB used, 1509.62 MB peak
                                // Process Virtual Memory: 0.00 MB used, 0.00 MB peak
                                // Physical Memory: 4286.43 MB used,  1375.50 MB free, 5661.93 MB total
                                // Virtual Memory: 1162.37 MB used,  1637.62 MB free, 2800.00 MB total
                                if (line.Contains("Physical Memory:") ||
                                    line.Contains("Virtual Memory:"))
                                {
                                    string[] strs = line.Split(':');
                                    if (strs.Length == 2)
                                    {
                                        string[] substrs = strs[1].Split(',');
                                        int segCount = substrs.Length;
                                        string GroupName = segCount == 2 ? "ProcessMemory" : "PlatformMemory";
                                        int GroupID = (int)ParseState.PLATFORM_MEM_STATS + (segCount == 2 ? 1 : 2);
                                        {
                                            for (int i = segCount - 1; i >= 0; --i)
                                            {
                                                int startID = 0;
                                                while (substrs[i][startID] == ' ')
                                                {
                                                    ++startID;
                                                }
                                                string repstr = substrs[i].Substring(startID);
                                                string[] tmpstrs = repstr.Split(' ');
                                                if (tmpstrs.Length == 3)
                                                {
                                                    float sizeMB;
                                                    if (float.TryParse(tmpstrs[0], out sizeMB))
                                                    {
                                                        GetRowEntry(GroupID, GroupName, strs[0]).Add(tmpstrs[2], fileName, sizeMB, tmpstrs[1]);
                                                        if(line.Contains("Physical Memory:") && segCount == 2)
                                                        {
                                                            if (i == 0)
                                                            {
                                                                GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", "Process Memory").Add("Size", fileName, sizeMB, tmpstrs[1]);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            break;

                        case ParseState.MALLOC_BINNED2_MEM:
                            {
                                // UE5:
                                // FMallocBinned2 Mem report
                                // Constants.BinnedPageSize = 65536
                                // Constants.BinnedAllocationGranularity = 4096
                                // Small Pool Allocations: 117.382751mb  (including block size padding)
                                // Small Pool OS Allocated: 151.875000mb
                                // Large Pool Requested Allocations: 122.291153mb
                                // Large Pool OS Allocated: 123.117188mb
                                // Requested Allocations: 122.291153mb
                                // OS Allocated: 123.117188mb
                                // PoolInfo: 1.500000mb
                                // Hash: 0.066406mb
                                // TLS: 0.039062mb
                                // Total allocated from OS: 276.597656mb
                                // Cached free OS pages: 339.312500mb
                                if (line.Contains(":") && line.Contains("mb"))
                                {
                                    string[] strs = line.Split(':');
                                    if (strs.Length == 2)
                                    {
                                        string key = strs[0];
                                        string strmb = strs[1].Substring(0, strs[1].IndexOf("mb")).Trim();
                                        float sizeMB;
                                        if (float.TryParse(strmb, out sizeMB))
                                        {
                                            GetRowEntry((int)ParseState.MALLOC_BINNED2_MEM, "FMallocBinned2 Mem", key).Add("SizeMB", fileName, sizeMB, "MB");
                                        }
                                    }
                                }
                            }
                            break;

                        case ParseState.LUA_MEMORY:
                            {
                                string[] words = line.Split(':');
                                if(words.Length == 2)
                                {
                                    string strkb = words[1].Replace(" ", "").Replace("(KB)", "");
                                    float kb = 0;
                                    if(float.TryParse(strkb, out kb))
                                    {
                                        // 改为通过unlua来统计
                                        //GetRowEntry((int)ParseState.LUA_MEMORY, "Lua Memory", "Lua Memory").Add("SizeMem", fileName, kb, "KB");
                                    }
                                }
                            }
                            break;

                        case ParseState.BRIEF_ANDROID_MEMINFO:
                            {
                                string kv = line.Replace("Android meminfo - ", "");
                                string[] keyValues = kv.Replace(" ", "").Split(':');
                                if(keyValues.Length == 2)
                                {
                                    string strv = keyValues[1].Replace("(MB)", "");
                                    float mb;
                                    if(float.TryParse(strv, out mb))
                                    {
                                        GetRowEntry((int)ParseState.BRIEF_ANDROID_MEMINFO, "Brief Android MemInfo", keyValues[0]).Add("SizeMem", fileName, mb, "MB");
                                    }
                                }
                            }
                            break;

                        case ParseState.RHI_RENDER_TARGET2D_INFO:
                            {
                                AddRHIDetailInfo(line, fileName, ParseState.RHI_RENDER_TARGET2D_INFO, "RHIRenderTarget2D");
                            }
                            break;

                        case ParseState.RHI_RENDER_TARGET3D_INFO:
                            {
                                AddRHIDetailInfo(line, fileName, ParseState.RHI_RENDER_TARGET3D_INFO, "RHIRenderTarget3D");
                            }
                            break;

                        case ParseState.RHI_RENDER_TARGETCUBE_INFO:
                            {
                                AddRHIDetailInfo(line, fileName, ParseState.RHI_RENDER_TARGETCUBE_INFO, "RHIRenderTargetCube");
                            }
                            break;

                        case ParseState.RHI_TEXTURE2D_INFO:
                            {
                                AddRHIDetailInfo(line, fileName, ParseState.RHI_TEXTURE2D_INFO, "RHITexture2D");
                            }
                            break;

                        case ParseState.RHI_TEXTURE3D_INFO:
                            {
                                AddRHIDetailInfo(line, fileName, ParseState.RHI_TEXTURE3D_INFO, "RHITexture3D");
                            }
                            break;

                        case ParseState.RHI_TEXTURECUBE_INFO:
                            {
                                AddRHIDetailInfo(line, fileName, ParseState.RHI_TEXTURECUBE_INFO, "RHITextureCube");
                            }
                            break;

                        case ParseState.WWISE_MEMORY:
                            {
                                string[] words = line.Split(' ');
                                if (words.Length == 5)
                                {
                                    int index = 1;
                                    {
                                        string[] substrs = words[index].Split('=');
                                        int value;
                                        if (int.TryParse(substrs[1], out value))
                                        {
                                            float mb = (float)(value) / 1024.0f / 1024.0f;
                                            GetRowEntry((int)ParseState.WWISE_MEMORY, "WWISE Memory", "Total").Add(substrs[0], fileName, mb, "MB");

                                            string key = "Group [Wwise]";
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", key).Add("Size", fileName, mb, "MB");
                                        }
                                    }
                                }
                                else if(words.Length == 6)
                                {
                                    string[] substrs = words[1].Split('=');
                                    string memID = substrs[1];
                                    int index = 2;
                                    {                                        
                                        substrs = words[index].Split('=');
                                        int value;
                                        if (int.TryParse(substrs[1], out value))
                                        {
                                            if (index <= 3)
                                            {
                                                float mb = (float)(value) / 1024.0f / 1024.0f;
                                                GetRowEntry((int)ParseState.WWISE_MEMORY, "WWISE Memory", memID).Add(substrs[0], fileName, mb, "MB");
                                            }
                                            else
                                            {
                                                GetRowEntry((int)ParseState.WWISE_MEMORY, "WWISE Memory", memID).Add(substrs[0], fileName, value, "");
                                            }
                                        }
                                    }
                                }
                            }
                            break;

                        case ParseState.TEXTURE_DISTRIBUTION:
                            {
                                string[] CATEGORIE_NAMES = { 
                                    "RHIRenderTarget2D", "RHIRenderTarget3D", "RHIRenderTargetCube", 
                                    "RHITexture2D", "RHITexture3D", "RHITextureCube", };
                                ParseState[] CATEGORIE_STATES = { 
                                    ParseState.TEXTURE_DISTRIBUTION_RenderTarget2D, ParseState.TEXTURE_DISTRIBUTION_RenderTarget3D, ParseState.TEXTURE_DISTRIBUTION_RenderTargetCube,
                                    ParseState.TEXTURE_DISTRIBUTION_Texture2D, ParseState.TEXTURE_DISTRIBUTION_Texture3D, ParseState.TEXTURE_DISTRIBUTION_TextureCube, };

                                if (line.Contains("Name,"))
                                    continue;
                                else if (line.Contains("Texture Memory Distribution Brief End"))
                                {
                                    state = ParseState.SEARCHING;
                                    continue;
                                }
                                string[] words = line.Split(',');
                                if (words.Length == 3)
                                {
                                    int count = int.Parse(words[1]);
                                    long mem = long.Parse(words[2].Replace("(byte)", ""));
                                    float mb = mem / 1024.0f / 1024.0f;
                                    string rowtag = "";
                                    bool isCat = false;
                                    for (int i = 0; i < 6; ++i)
                                    {
                                        if (words[0].Contains(CATEGORIE_NAMES[i]))
                                        {
                                            texDistCat = CATEGORIE_NAMES[i];
                                            rowtag = "[TotalStatistic]";
                                            catState = CATEGORIE_STATES[i];
                                            isCat = true;
                                            break;
                                        }
                                    }
                                    if (isCat == false)
                                    {
                                        rowtag = words[0];
                                    }
                                    GetRowEntry((int)catState, texDistCat + "DistributionBrief", rowtag).Add("Count", fileName, count, "");
                                    GetRowEntry((int)catState, texDistCat + "DistributionBrief", rowtag).Add("MemSize(MB)", fileName, mb, "(MB)");
                                }
                            }
                            break;
                    }
                }
            }
            SetGroupNeedCalcTotal("Lua Memory");
            SetGroupNeedCalcTotal("RHI Memory");
            SetGroupNeedCalcTotal("TextureGroupInMem");
            SetGroupNeedCalcTotal("TextureFormatInMem");
            SetGroupNeedCalcTotal("TextureInMem");
            SetGroupNeedCalcTotal("Render Target Pools");
            SetGroupNeedCalcTotal("Brief UE MemInfo");
            SetGroupNeedCalcTotal("RHIRenderTarget2D");
            SetGroupNeedCalcTotal("RHIRenderTarget3D");
            SetGroupNeedCalcTotal("RHIRenderTargetCube");
            SetGroupNeedCalcTotal("RHITexture2D");
            SetGroupNeedCalcTotal("RHITexture3D");
            SetGroupNeedCalcTotal("RHITextureCube");
            //SetGroupNeedCalcTotal("WWISE Memory");
        }

        void AddRHIDetailInfo(string line, string fileName, ParseState state, string groupName)
        {
            const float invMB = 1.0f / 1024.0f / 1024.0f;
            string[] words = line.Split(", ");
            if (words.Length == 3)
            {
                string key = words[0] + " " + words[1];
                string b = words[2].Replace("(byte)", "");
                int nb;
                if (int.TryParse(b, out nb))
                {
                    GetRowEntry((int)state, groupName, key).Add("SizeMem", fileName, nb * invMB, "(MB)");
                }
            }
        }

        public void DataRegulate(ref List<string> FileNameLst)
        {
            ///////////////////////////////////////////////////////////////////////
            /// 数据校对并计算差异            
            foreach (var itAll in AllDatas)
            {
                GroupEntry ge = itAll.Value;
                Dictionary<string, RowEntry> rows = itAll.Value.RowDatas;
                RowEntry TotalRow = ge.NeedCalcTotal ? new RowEntry() : null;
                if (TotalRow != null)
                {
                    TotalRow.RowName = "[TotalStatistics]";
                }
                foreach (var itRow in rows)
                {
                    RowEntry row = itRow.Value;
                    if (TotalRow != null && TotalRow.ColumeEntries.Count == 0)
                    {
                        for (int i = 0; i < row.ColumeEntries.Count; ++i)
                        {
                            ColumeEntry srcCE = row.ColumeEntries[i];
                            ColumeEntry tarCE = new ColumeEntry();
                            tarCE.ColumeName = srcCE.ColumeName;
                            tarCE.ColumeTag = srcCE.ColumeTag;
                            TotalRow.ColumeEntries.Add(tarCE);
                        }
                    }

                    for (int colIndex = 0; colIndex < row.ColumeEntries.Count; ++colIndex)
                    {
                        var srcCol = row.ColumeEntries[colIndex];
                        if (srcCol.Entries.Count > 0)
                        {
                            if (srcCol.Entries.Count != FileNameLst.Count)
                            {
                                for (int i = 0; i < FileNameLst.Count; ++i)
                                {
                                    if (i >= srcCol.Entries.Count)
                                    {
                                        srcCol.Add(FileNameLst[i], 0);
                                    }
                                    else if (srcCol.Entries[i].MemReportFileName != FileNameLst[i])
                                    {
                                        srcCol.Entries.Insert(i, new Entry(FileNameLst[i], 0));
                                    }
                                }
                            }
                            srcCol.CalcDiff();

                            if (TotalRow != null)
                            {
                                var tarCol = TotalRow.ColumeEntries[colIndex];
                                bool hasInit = tarCol.Entries.Count > 0;
                                for (int i = 0; i < srcCol.Entries.Count; ++i)
                                {
                                    Entry srcEntry = srcCol.Entries[i];
                                    Entry tarEntry = null;
                                    if (hasInit == false)
                                    {
                                        tarEntry = new Entry(srcEntry.MemReportFileName, srcEntry.Value);
                                        tarCol.Entries.Add(tarEntry);
                                    }
                                    else
                                    {
                                        tarEntry = tarCol.Entries[i];
                                        tarEntry.Value += srcEntry.Value;
                                    }
                                }
                            }
                        }

                        if (srcCol.ExtraInfos.Count > 0)
                        {
                            if (srcCol.ExtraInfos.Count != FileNameLst.Count)
                            {
                                for (int i = 0; i < FileNameLst.Count; ++i)
                                {
                                    if (i >= srcCol.ExtraInfos.Count)
                                    {
                                        srcCol.AddExtraInfo(FileNameLst[i], "0");
                                    }
                                    else if (srcCol.ExtraInfos[i].MemReportFileName != FileNameLst[i])
                                    {
                                        srcCol.ExtraInfos.Insert(i, new ColumeExtraInfo(FileNameLst[i], "0"));
                                    }
                                }
                            }

                            if (TotalRow != null)
                            {
                                var tarCol = TotalRow.ColumeEntries[colIndex];
                                bool hasInit = tarCol.ExtraInfos.Count > 0;
                                if (hasInit == false)
                                {
                                    for (int i = 0; i < srcCol.ExtraInfos.Count; ++i)
                                    {
                                        ColumeExtraInfo srcEntry = srcCol.ExtraInfos[i];
                                        ColumeExtraInfo tarEntry = new ColumeExtraInfo(srcEntry.MemReportFileName, "0");
                                        tarCol.ExtraInfos.Add(tarEntry);
                                    }
                                }
                            }
                        }
                    }
                }
                if (TotalRow != null)
                {
                    for (int i = 0; i < TotalRow.ColumeEntries.Count; ++i)
                    {
                        TotalRow.ColumeEntries[i].CalcDiff();
                    }
                    ge.RowDatas.Add(TotalRow.RowName, TotalRow);
                }
            }
        }

        public void AnalyzeAndOutputResults(ref List<string> FileNameLst, ref List<string> FileFullPathLst, string outputCsvFilePath)
        {
            GenDataFromFiles(ref FileNameLst, ref FileFullPathLst);
            DataRegulate(ref FileNameLst);

            ///////////////////////////////////////////////////////////////////////
            /// 对数据组进行排序，方便在报表里按顺序输出
            List<GroupEntry> SortGroups = new List<GroupEntry>();
            foreach (var itAll in AllDatas)
            {
                SortGroups.Add(itAll.Value);
            }
            SortGroups.Sort((a, b) =>
            {
                if (a.GroupID < b.GroupID)
                    return -1;
                return 1;
            });
            ///////////////////////////////////////////////////////////////////////

            ///////////////////////////////////////////////////////////////////////
            /// 输出对比文件
            if(EXPORT_AS_MULTISHEETS_EXCEL)
            {
                List<string> ColNames = new List<string>();
                for(char i = 'A'; i <= 'Z'; ++i)
                {
                    ColNames.Add(i.ToString());
                }

                Workbook workbook = new Workbook();
                workbook.Worksheets.Clear();

                foreach (var group in SortGroups)
                {
                    string GroupName = group.GroupName;
                    Worksheet workSheet = workbook.CreateEmptySheet();
                    workSheet.Name = GroupName;

                    // 对Group里的每一行进行收集然后做排序
                    List<RowEntry> rowLst = new List<RowEntry>();
                    foreach (var itRow in group.RowDatas)
                    {
                        rowLst.Add(itRow.Value);
                    }
                    rowLst.Sort((a, b) =>
                    {
                        ColumeEntry ceA = a.ColumeEntries[a.ColumeEntries.Count - 1];
                        ColumeEntry ceB = b.ColumeEntries[b.ColumeEntries.Count - 1];
                        if (ceA.Diff > ceB.Diff)
                            return -1;
                        return 1;
                    });

                    // 打印头信息
                    var firstRow = rowLst[0];
                    int rowCount = 0;
                    int colCount = 0;
                    {
                        BuiltInStyles HeadStyle = BuiltInStyles.Good;
                        rowCount = 1;
                        string ColName = ColNames[colCount++] + (rowCount).ToString();
                        workSheet.Range[ColName].Value = "Type";
                        workSheet.Range[ColName].BuiltInStyle = HeadStyle;

                        foreach (var ce in firstRow.ColumeEntries)
                        {
                            foreach (var ei in ce.ExtraInfos)
                            {
                                ColName = ColNames[colCount++] + (rowCount).ToString();
                                workSheet.Range[ColName].Value = string.Format("{0}_[{1}]", ce.ColumeName, ei.MemReportFileName);
                                workSheet.Range[ColName].BuiltInStyle = HeadStyle;
                                
                            }
                            foreach (var e in ce.Entries)
                            {
                                ColName = ColNames[colCount++] + (rowCount).ToString();
                                workSheet.Range[ColName].Value = string.Format("{0}[{1}]_[{2}]", ce.ColumeName, ce.ColumeTag, e.MemReportFileName);
                                workSheet.Range[ColName].BuiltInStyle = HeadStyle;
                                
                            }
                            if (ce.Entries.Count > 1)
                            {
                                ColName = ColNames[colCount++] + (rowCount).ToString();
                                workSheet.Range[ColName].Value = string.Format("Diff_{0}[{1}]", ce.ColumeName, ce.ColumeTag);
                                workSheet.Range[ColName].BuiltInStyle = HeadStyle;                                
                            }
                        }
                        ++rowCount;
                    }
                    
                    // 对排序后的数据进行打印
                    foreach (var row in rowLst)
                    {
                        colCount = 0;
                        string ColName = ColNames[colCount++] + (rowCount).ToString();
                        workSheet.Range[ColName].Value = row.RowName;

                        foreach (var col in row.ColumeEntries)
                        {
                            foreach (var ei in col.ExtraInfos)
                            {
                                ColName = ColNames[colCount++] + (rowCount).ToString();
                                workSheet.Range[ColName].Value = ei.ColumeValue;
                            }
                            foreach (var e in col.Entries)
                            {
                                ColName = ColNames[colCount++] + (rowCount).ToString();
                                workSheet.Range[ColName].NumberValue = (float)Math.Round(e.Value, 3);
                            }
                            if (col.Entries.Count > 1)
                            {
                                ColName = ColNames[colCount++] + (rowCount).ToString();
                                workSheet.Range[ColName].NumberValue = (float)Math.Round(col.Diff, 3);
                            }
                        }

                        ++rowCount;
                    }
                }

                // 将 Excel 文件保存到磁盘
                workbook.SaveToFile(outputCsvFilePath, ExcelVersion.Version97to2003);
                // 释放资源
                workbook.Dispose();

                Console.WriteLine("Analyze Completed! Result File : " + outputCsvFilePath);
            }
            else
            {
                System.IO.File.Delete(outputCsvFilePath);
                FileStream fs = new FileStream(outputCsvFilePath, FileMode.OpenOrCreate);
                StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);

                foreach (var group in SortGroups)
                {
                    string GroupName = group.GroupName;
                    
                    // 对Group里的每一行进行收集然后做排序
                    List<RowEntry> rowLst = new List<RowEntry>();
                    foreach (var itRow in group.RowDatas)
                    {
                        rowLst.Add(itRow.Value);
                    }
                    rowLst.Sort((a, b) =>
                    {
                        ColumeEntry ceA = a.ColumeEntries[a.ColumeEntries.Count - 1];
                        ColumeEntry ceB = b.ColumeEntries[b.ColumeEntries.Count - 1];
                        if (ceA.Diff > ceB.Diff)
                            return -1;
                        return 1;
                    });
                    
                    // 打印头信息
                    string strRow = GroupName + ", ";
                    var firstRow = rowLst[0];
                    {
                        foreach (var ce in firstRow.ColumeEntries)
                        {
                            foreach (var ei in ce.ExtraInfos)
                            {
                                strRow += string.Format("{0}_[{1}], ", ce.ColumeName, ei.MemReportFileName);
                            }
                            foreach (var e in ce.Entries)
                            {
                                strRow += string.Format("{0}[{1}]_[{2}], ", ce.ColumeName, ce.ColumeTag, e.MemReportFileName);
                            }
                            if (ce.Entries.Count > 1)
                            {
                                strRow += string.Format("Diff_{0}[{1}], ", ce.ColumeName, ce.ColumeTag);
                            }
                        }
                    }
                    Console.WriteLine(strRow);
                    sw.WriteLine(strRow);
                    sw.Flush();

                    // 对排序后的数据进行打印
                    foreach (var row in rowLst)
                    {
                        strRow = row.RowName + ", ";
                        foreach (var col in row.ColumeEntries)
                        {
                            foreach (var ei in col.ExtraInfos)
                            {
                                strRow += ei.ColumeValue + ", ";
                            }
                            foreach (var e in col.Entries)
                            {
                                strRow += (float)Math.Round(e.Value, 3) + ", ";
                            }
                            if (col.Entries.Count > 1)
                            {
                                strRow += (float)Math.Round(col.Diff, 3) + ", ";
                            }
                        }

                        Console.WriteLine(strRow);
                        sw.WriteLine(strRow);
                        sw.Flush();
                    }

                    Console.WriteLine();
                    sw.WriteLine("\n");
                    sw.Flush();
                }
                sw.Close();
                fs.Close();
            }
            Console.WriteLine("Analyze Completed! Result File : " + outputCsvFilePath);
        }

        public void GenerateDataTree()
        {
            var AndroidGroup = GetGroupEntry("Brief Android MemInfo");
            if(AndroidGroup != null)
            {
                EntryTreeNode node = new EntryTreeNode();
                var row = AndroidGroup.GetRowEntry("summary.total-pss");
                node.NodeDesc = AndroidGroup.GroupName;
                int colCount = row.ColumeEntries.Count;
                ColumeEntry ce = row.ColumeEntries[colCount - 1];
                node.MemSizeMB = ce.Entries[ce.Entries.Count - 1].Value;
                node.GroupData = AndroidGroup;

                DataTree.Add(node);
            }

            var UEGroup = GetGroupEntry("Brief UE MemInfo");
            if(UEGroup != null)
            {
                EntryTreeNode nodeUE = new EntryTreeNode();
                var row = UEGroup.GetRowEntry("[TotalStatistics]");
                nodeUE.NodeDesc = UEGroup.GroupName;
                int colCount = row.ColumeEntries.Count;
                ColumeEntry ce = row.ColumeEntries[colCount - 1];
                nodeUE.MemSizeMB = ce.Entries[ce.Entries.Count - 1].Value;
                nodeUE.GroupData = UEGroup;

                foreach(var key in UEGroup.RowDatas.Keys)
                {
                    if (key.Equals("[TotalStatistics]"))
                        continue;

                    row = UEGroup.RowDatas[key];
                    string[] words = key.Split(":");
                    string groupName = words[0];
                    colCount = row.ColumeEntries.Count;
                    ce = row.ColumeEntries[colCount - 1];
                    float MemSizeMB = ce.Entries[ce.Entries.Count - 1].Value;

                    EntryTreeNode nodeGroup = null;
                    if (nodeUE.SubNodes.Count == 0)
                    {
                        nodeGroup = new EntryTreeNode();
                        nodeGroup.NodeDesc = groupName;
                        nodeGroup.MemSizeMB = MemSizeMB;
                    }
                    else
                    {
                        foreach(var subNode in nodeUE.SubNodes)
                        {
                            if(subNode.NodeDesc == groupName)
                            {
                                nodeGroup = subNode;
                                break;
                            }
                        }
                        nodeGroup.MemSizeMB += MemSizeMB;
                    }
                    EntryTreeNode nodeSub = new EntryTreeNode();
                    nodeSub.NodeDesc = key;
                    nodeSub.MemSizeMB = MemSizeMB;
                    nodeGroup.SubNodes.Add(nodeSub);

                    if(groupName.Equals("Group [UObject]"))
                    {
                        nodeGroup.GroupData = GetGroupEntry("Obj Classes");
                    }
                    else if(groupName.Equals("Group [RHI]"))
                    {
                        nodeGroup.GroupData = GetGroupEntry("RHI Memory");
                    }
                }
            }
        }
    }

}