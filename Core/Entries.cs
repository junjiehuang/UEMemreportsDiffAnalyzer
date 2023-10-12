
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
        POOLED_RENDER_TARGETS = 700,
        TEXTURE_LIST = 800,
        OBJECT_CLASS_LIST = 900,
        OBJECT_LIST = 1000,
        PERSISTENT_LEVEL_SPAWNED_ACTORS = 1100,
        LUA_MEMORY = 1200,        
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
    }

    // 管理所有组的对比数据
    public class DataManager
    {
        public static bool EXPORT_AS_MULTISHEETS_EXCEL = true;

        public Dictionary<string, GroupEntry> AllDatas = new Dictionary<string, GroupEntry>();

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
                    else if(line.Contains("Lua memory usage"))
                    {
                        state = ParseState.LUA_MEMORY;
                    }
                    else if(line.Contains("Android meminfo"))
                    {
                        state = ParseState.BRIEF_ANDROID_MEMINFO;
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
                                    float inMemSize = 0;
                                    float onDiskSize = 0;
                                    if (ParseInMemOnDisk(line, out inMemSize, out onDiskSize))
                                    {
                                        //string key = "Total";
                                        //GetRowEntry(((int)ParseState.TEXTURE_LIST + 1), "TextureTotal In Mem", key).Add("InMemSize", fileName, inMemSize, "MB");
                                        //GetRowEntry(((int)ParseState.TEXTURE_LIST + 2), "TextureTotal On Disk", key).Add("OnDiskSize", fileName, onDiskSize, "MB");
                                        GetRowEntry(((int)ParseState.TEXTURE_LIST + 1), "Texture Total", "In Memory").Add("Size", fileName, inMemSize, "MB");
                                        GetRowEntry(((int)ParseState.TEXTURE_LIST + 1), "Texture Total", "On Disk").Add("Size", fileName, onDiskSize, "MB");

                                        GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", "Texture Total").Add("Size", fileName, inMemSize, "MB");
                                    }
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
                                                GetRowEntry(((int)ParseState.TEXTURE_LIST + 3), "TextureGroup In Mem", key).Add("InMemSize", fileName, inMemSize, "MB");
                                                //GetRowEntry(((int)ParseState.TEXTURE_LIST + 4), "TextureGroup On Disk", key).Add("OnDiskSize", fileName, onDiskSize, "MB");
                                            }
                                            if (line.Contains("Total PF_"))
                                            {
                                                GetRowEntry(((int)ParseState.TEXTURE_LIST + 5), "TextureFormat In Mem", key).Add("InMemSize", fileName, inMemSize, "MB");
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
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).AddExtraColume("Width x Height", fileName, inMemSize[0]);
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).AddExtraColume("LODGroup", fileName, words[4]);
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).AddExtraColume("Streaming", fileName, Streaming.ToString());
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).AddExtraColume("Uncompressed", fileName, Uncompressed.ToString());
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).Add("NumMips", fileName, int.Parse(words[10]), "");
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).Add("SizeInMem", fileName, inMemSizeKB, "KB");
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
                                                            GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).AddExtraColume("Width x Height", fileName, inMemSize[0]);
                                                            GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).AddExtraColume("LODGroup", fileName, words[4]);
                                                            GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).AddExtraColume("Streaming", fileName, Streaming.ToString());
                                                            GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).Add("SizeInMem", fileName, inMemSizeKB, "KB");
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
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).AddExtraColume("Width x Height", fileName, inMemSize[0]);
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).AddExtraColume("LODGroup", fileName, words[4]);
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).AddExtraColume("Streaming", fileName, Streaming.ToString());
                                                    GetRowEntry(((int)ParseState.TEXTURE_LIST + 7), "Texture In Mem", key).Add("SizeInMem", fileName, inMemSizeKB, "KB");
                                                    //AddEntry(GetStatDict(allStats, "Texture In Mem"), fileName, key, inMemSizeKB, "KB");
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
                            {
                                if (line.Contains("Current Memory"))
                                {
                                    //Current Memory 1553.98 MB used, plus 98.58 MB waste
                                    ParseBinnedMemory2(line, "Current Memory ", "Used", "Waste", fileName);
                                }
                                else if (line.Contains("Peak Memory"))
                                {
                                    //Peak Memory 1556.45 MB used, plus 99.49 MB waste
                                    ParseBinnedMemory2(line, "Peak Memory ", "Used", "Waste", fileName);
                                }
                                else if (line.Contains("Current OS Memory"))
                                {
                                    //Current OS Memory 1652.56 MB, peak 1655.94 MB
                                    ParseBinnedMemory2(line, "Current OS Memory ", "Used", "Peak", fileName);
                                }
                                else if (line.Contains("Current Waste"))
                                {
                                    //Current Waste 35.56 MB, peak 35.74 MB
                                    ParseBinnedMemory2(line, "Current Waste ", "Waste", "Peak", fileName);
                                }
                                else if (line.Contains("Current Used"))
                                {
                                    //Current Used 1553.98 MB, peak 1556.45 MB
                                    ParseBinnedMemory2(line, "Current Used ", "Used", "Peak", fileName);
                                }
                                else if (line.Contains("Current Slack"))
                                {
                                    //Current Slack 63.03 MB
                                    ParseBinnedMemory2(line, "Current Slack ", "Used", "Peak", fileName);
                                }
                            }
                            break;

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
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", "UObject Memory").Add("Size", fileName, objMB, "MB");
                                        }

                                        words = segs[2].Split(' ');
                                        string strResMB = words[2].Replace("M", "");
                                        float resMB;
                                        if (float.TryParse(strResMB, out resMB))
                                        {
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", "UObject Reference Res Memory").Add("Size", fileName, resMB, "MB");
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
                                            float numKB;
                                            if (float.TryParse(words[2], out numKB))
                                            {
                                                string key = words[1];
                                                GetRowEntry(GroupID, srGroupName, key).Add("NumKB", fileName, numKB, "KB");
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
                                                float numKB;
                                                if (float.TryParse(words[2], out numKB))
                                                {
                                                    string key = words[1];
                                                    GetRowEntry(GroupID, srGroupName, key).Add("NumKB", fileName, numKB, "KB");
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
                                else if(line.Contains("total"))
                                {
                                    words = SplitAndTrim(line, ' ');
                                    if(words.Length == 2)
                                    {
                                        string strmb = words[0].Replace("MB", "");
                                        float mb;
                                        if(float.TryParse(strmb, out mb))
                                        {
                                            GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", "RHI Memory").Add("Size", fileName, mb, "MB");
                                        }
                                    }
                                }
                                
                            }
                            break;

                        case ParseState.MEMORY_STATS:
                            {
                                string[] words = SplitAndTrim(line, '-');
                                if (words.Length >= 3)
                                {
                                    string key = words[2];
                                    float value;
                                    bool hasMB = words[0].Contains("MB");
                                    if (hasMB)
                                        words[0] = words[0].Replace("MB", "");
                                    if (float.TryParse(words[0], out value))
                                    {
                                        if (hasMB == false)
                                            value /= (1024 * 1024);
                                        GetRowEntry((int)ParseState.MEMORY_STATS, "Stat Memory", key).Add("SizeMem", fileName, value, "MB");
                                    }

                                    // 把unlua的内存加入到简报里
                                    // unlua 容器分配的内存
                                    if(line.Contains("STAT_UnLua_ContainerElementCache_Memory"))
                                    {
                                        GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", "UnLua ContainerElementCache Memory").Add("Size", fileName, value, "MB");
                                        GetRowEntry((int)ParseState.LUA_MEMORY, "Lua Memory", "UnLua ContainerElementCache Memory").Add("Size", fileName, value, "MB");
                                    }
                                    // unlua lua内存分配器分配的内存
                                    else if (line.Contains("STAT_UnLua_Lua_Memory"))
                                    {
                                        GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", "UnLua Lua Memory").Add("Size", fileName, value, "MB");
                                        GetRowEntry((int)ParseState.LUA_MEMORY, "Lua Memory", "UnLua Lua Memory").Add("Size", fileName, value, "MB");
                                    }
                                    // unlua 为 function parameter 分配的内存
                                    else if (line.Contains("STAT_UnLua_PersistentParamBuffer_Memory"))
                                    {
                                        GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", "UnLua PersistentParamBuffer Memory").Add("Size", fileName, value, "MB");
                                        GetRowEntry((int)ParseState.LUA_MEMORY, "Lua Memory", "UnLua PersistentParamBuffer Memory").Add("Size", fileName, value, "MB");
                                    }
                                    // unlua 为 function out parameter 分配的内存
                                    else if (line.Contains("STAT_UnLua_OutParmRec_Memory"))
                                    {
                                        GetRowEntry((int)ParseState.BRIEF_UE_MEMORY_INFO, "Brief UE MemInfo", "UnLua OutParmRec Memory").Add("Size", fileName, value, "MB");
                                        GetRowEntry((int)ParseState.LUA_MEMORY, "Lua Memory", "UnLua OutParmRec Memory").Add("Size", fileName, value, "MB");
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
                    }
                }
            }
            SetGroupNeedCalcTotal("Lua Memory");
            SetGroupNeedCalcTotal("RHI Memory");
            SetGroupNeedCalcTotal("Texture In Mem");
            SetGroupNeedCalcTotal("Render Target Pools");
        }

        public void AnalyzeAndOutputResults(ref List<string> FileNameLst, ref List<string> FileFullPathLst, string outputCsvFilePath)
        {
            GenDataFromFiles(ref FileNameLst, ref FileFullPathLst);

            ///////////////////////////////////////////////////////////////////////
            /// 数据校对并计算差异
            List<GroupEntry> SortGroups = new List<GroupEntry>();
            foreach (var itAll in AllDatas)
            {
                GroupEntry ge = itAll.Value;
                SortGroups.Add(ge);
                Dictionary<string, RowEntry> rows = itAll.Value.RowDatas;
                RowEntry TotalRow = ge.NeedCalcTotal ? new RowEntry() : null;
                if(TotalRow != null)
                {
                    TotalRow.RowName = "[TotalStatistics]";
                }
                foreach (var itRow in rows)
                {
                    RowEntry row = itRow.Value;
                    if(TotalRow!=null && TotalRow.ColumeEntries.Count == 0)
                    {
                        for(int i = 0; i < row.ColumeEntries.Count; ++i)
                        {
                            ColumeEntry srcCE = row.ColumeEntries[i];
                            ColumeEntry tarCE = new ColumeEntry();
                            tarCE.ColumeName = srcCE.ColumeName;
                            tarCE.ColumeTag = srcCE.ColumeTag;
                            TotalRow.ColumeEntries.Add(tarCE);
                        }
                    }

                    //foreach (var col in row.ColumeEntries)
                    for(int colIndex = 0; colIndex < row.ColumeEntries.Count; ++colIndex)
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

                            if(TotalRow!=null)
                            {
                                var tarCol = TotalRow.ColumeEntries[colIndex];
                                bool hasInit = tarCol.Entries.Count > 0;
                                for(int i = 0; i < srcCol.Entries.Count; ++i)
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
                                if(hasInit == false)
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
                if(TotalRow!=null)
                {
                    for(int i = 0; i < TotalRow.ColumeEntries.Count; ++i)
                    {
                        TotalRow.ColumeEntries[i].CalcDiff();
                    }
                    ge.RowDatas.Add(TotalRow.RowName, TotalRow);
                }
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
                //IWorkbook workbook = new HSSFWorkbook();

                foreach (var group in SortGroups)
                {
                    string GroupName = group.GroupName;
                    //ISheet worksheet = workbook.CreateSheet(GroupName);
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
                        //IRow newRow = worksheet.CreateRow(rowCount++);
                        //newRow.CreateCell(colCount++).SetCellValue("Type");

                        BuiltInStyles HeadStyle = BuiltInStyles.Good;
                        rowCount = 1;
                        string ColName = ColNames[colCount++] + (rowCount).ToString();
                        workSheet.Range[ColName].Value = "Type";
                        workSheet.Range[ColName].BuiltInStyle = HeadStyle;
                        //IBorders borders = workSheet.Range[ColName].Borders;
                        //borders.LineStyle = LineStyleType.Thick;
                        //borders.Color = System.Drawing.Color.AliceBlue;

                        foreach (var ce in firstRow.ColumeEntries)
                        {
                            foreach (var ei in ce.ExtraInfos)
                            {
                                //newRow.CreateCell(colCount++).SetCellValue(string.Format("{0}_[{1}]", ce.ColumeName, ei.MemReportFileName));
                                ColName = ColNames[colCount++] + (rowCount).ToString();
                                workSheet.Range[ColName].Value = string.Format("{0}_[{1}]", ce.ColumeName, ei.MemReportFileName);
                                workSheet.Range[ColName].BuiltInStyle = HeadStyle;
                                //borders = workSheet.Range[ColName].Borders;
                                //borders.LineStyle = LineStyleType.Thick;
                                //borders.Color = System.Drawing.Color.AliceBlue;
                                
                            }
                            foreach (var e in ce.Entries)
                            {
                                //newRow.CreateCell(colCount++).SetCellValue(string.Format("{0}[{1}]_[{2}]", ce.ColumeName, ce.ColumeTag, e.MemReportFileName));
                                ColName = ColNames[colCount++] + (rowCount).ToString();
                                workSheet.Range[ColName].Value = string.Format("{0}[{1}]_[{2}]", ce.ColumeName, ce.ColumeTag, e.MemReportFileName);
                                workSheet.Range[ColName].BuiltInStyle = HeadStyle;
                                //borders = workSheet.Range[ColName].Borders;
                                //borders.LineStyle = LineStyleType.Thick;
                                //borders.Color = System.Drawing.Color.AliceBlue;
                                
                            }
                            if (ce.Entries.Count > 0)
                            {
                                //newRow.CreateCell(colCount++).SetCellValue(string.Format("Diff_{0}[{1}]", ce.ColumeName, ce.ColumeTag));
                                ColName = ColNames[colCount++] + (rowCount).ToString();
                                workSheet.Range[ColName].Value = string.Format("Diff_{0}[{1}]", ce.ColumeName, ce.ColumeTag);
                                workSheet.Range[ColName].BuiltInStyle = HeadStyle;
                                //borders = workSheet.Range[ColName].Borders;
                                //borders.LineStyle = LineStyleType.Thick;
                                //borders.Color = System.Drawing.Color.AliceBlue;
                                
                            }
                        }
                        ++rowCount;
                    }
                    
                    // 对排序后的数据进行打印
                    foreach (var row in rowLst)
                    {
                        colCount = 0;
                        //IRow newRow = worksheet.CreateRow(rowCount++);
                        //newRow.CreateCell(colCount++).SetCellValue(row.RowName);

                        string ColName = ColNames[colCount++] + (rowCount).ToString();
                        workSheet.Range[ColName].Value = row.RowName;

                        foreach (var col in row.ColumeEntries)
                        {
                            foreach (var ei in col.ExtraInfos)
                            {
                                //newRow.CreateCell(colCount++).SetCellValue(ei.ColumeValue);
                                ColName = ColNames[colCount++] + (rowCount).ToString();
                                workSheet.Range[ColName].Value = ei.ColumeValue;
                            }
                            foreach (var e in col.Entries)
                            {
                                //newRow.CreateCell(colCount++).SetCellValue(((float)Math.Round(e.Value, 3)));
                                ColName = ColNames[colCount++] + (rowCount).ToString();
                                workSheet.Range[ColName].NumberValue = (float)Math.Round(e.Value, 3);
                            }
                            if (col.Entries.Count > 0)
                            {
                                //newRow.CreateCell(colCount++).SetCellValue(((float)Math.Round(col.Diff, 3)));
                                ColName = ColNames[colCount++] + (rowCount).ToString();
                                workSheet.Range[ColName].NumberValue = (float)Math.Round(col.Diff, 3);
                            }
                        }

                        ++rowCount;
                    }
                }

                // 将 Excel 文件保存到磁盘
                workbook.SaveToFile(outputCsvFilePath, ExcelVersion.Version97to2003);
                //using (FileStream fs = new FileStream(outputCsvFilePath, FileMode.Create, FileAccess.Write))
                //{
                //    workbook.Write(fs);                    
                //    fs.Flush();
                //    fs.Close();
                //    fs.Dispose();
                //}
                // 释放资源
                workbook.Dispose();
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
                            if (ce.Entries.Count > 0)
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
                            if (col.Entries.Count > 0)
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
            Console.WriteLine("Analyze Completed!");
        }

    }

    public class Analyzer
    {
        public static string DoAnalyze(string[] args)
        {
            for (int i = 0; i < args.Length; ++i)
            {
                Console.WriteLine(args[i]);
            }
            ArgParserLite argParser = new ArgParserLite(args);
            DataManager dataMgr = new DataManager();

            List<string> FileNameLst = new List<string>();
            List<string> FileFullPathLst = new List<string>();
            // 解析第一个memreport文件
            string memReportPath1 = argParser.GetValue("-1f", null);
            // 解析第二个memreport文件
            string memReportPath2 = argParser.GetValue("-2f", null);
            // 解析差量分析的结果文件
            string diffCSVPath = argParser.GetValue("-o", null);
            // 差量模式
            string pattern = argParser.GetValue("-p", null);
            if (string.IsNullOrEmpty(memReportPath1) == false)
            {
                FileNameLst.Add(System.IO.Path.GetFileNameWithoutExtension(memReportPath1));
                FileFullPathLst.Add(memReportPath1);
                Console.WriteLine(string.Format("MemReport File [{0}]: {1}", FileNameLst.Count, memReportPath1));
            }
            if (string.IsNullOrEmpty(memReportPath2) == false)
            {
                FileNameLst.Add(System.IO.Path.GetFileNameWithoutExtension(memReportPath2));
                FileFullPathLst.Add(memReportPath2);
                Console.WriteLine(string.Format("MemReport File [{0}]: {1}", FileNameLst.Count, memReportPath2));
            }
            if (string.IsNullOrEmpty(pattern) == false)
            {
                DataManager.EXPORT_AS_MULTISHEETS_EXCEL = pattern.Equals("xls", StringComparison.CurrentCultureIgnoreCase);
                pattern = DataManager.EXPORT_AS_MULTISHEETS_EXCEL ? "xls" : pattern;
            }
            else
            {
                DataManager.EXPORT_AS_MULTISHEETS_EXCEL = true;
                pattern = "xls";
            }
            if (string.IsNullOrEmpty(diffCSVPath) == false)
            {
                if (FileNameLst.Count == 2)
                    diffCSVPath += string.Format("/Diff_{0}-{1}.{2}", FileNameLst[1], FileNameLst[0], pattern);
                else if (FileNameLst.Count == 1)
                    diffCSVPath += string.Format("/Diff_{0}.{1}", FileNameLst[0], pattern);
                Console.WriteLine(string.Format("Compare Result File [{0}]: ", diffCSVPath));
            }
            else
            {
                if (FileNameLst.Count == 2)
                    diffCSVPath = System.IO.Directory.GetCurrentDirectory() + string.Format("/Diff_{0}-{1}.{2}", FileNameLst[1], FileNameLst[0], pattern);
                else if (FileNameLst.Count == 1)
                    diffCSVPath = System.IO.Directory.GetCurrentDirectory() + string.Format("/Diff_{0}.{1}", FileNameLst[0], pattern);
                Console.WriteLine(string.Format("Compare Result File [{0}]: ", diffCSVPath));
            }

            if (FileNameLst.Count == 0 || argParser.GetOption("-h"))
            {
                Console.WriteLine("Parse UE4 MemReport files to see memory usage over time. ");
                Console.WriteLine("Analyze 2 memreports and output the diff to file.");
                Console.WriteLine("");
                Console.WriteLine("Usage:");
                Console.WriteLine("  -1f <memreport full path> Specify the first memreport file full path.");
                Console.WriteLine("     eg: -1f D:/UEProjs/ue4memreportparser/MemReports/01-17.19.54.memreport");
                Console.WriteLine("");
                Console.WriteLine("  -2f <memreport full path> Specify the second memreport file full path.");
                Console.WriteLine("     eg: -2f D:/UEProjs/ue4memreportparser/MemReports/30-18.40.46.memreport");
                Console.WriteLine("");
                Console.WriteLine("  -o <compare result csv folder path> ");
                Console.WriteLine("     eg: -o D:/UEProjs/ue4memreportparser/MemReports");
                Console.WriteLine("");
                Console.WriteLine("  -p <file extension> ");
                Console.WriteLine("     eg: -p xls");
                Console.WriteLine("");
                Console.WriteLine("  -h <help info>");
                return null;
            }

            // 解析并输出结果
            dataMgr.AnalyzeAndOutputResults(ref FileNameLst, ref FileFullPathLst, diffCSVPath);
            return diffCSVPath;
        }
    }
}