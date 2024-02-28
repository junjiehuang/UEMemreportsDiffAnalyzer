using MemReportParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{

    public class Analyzer
    {
        public static void ParseArgs(string[] args, ArgParserLite argParser, out List<string> FileNameLst, out List<string> FileFullPathLst, out string diffCSVPath)
        {
            for (int i = 0; i < args.Length; ++i)
            {
                Console.WriteLine(args[i]);
            }
            FileNameLst = new List<string>();
            FileFullPathLst = new List<string>();
            // 解析第一个memreport文件
            string memReportPath1 = argParser.GetValue("-1f", null);
            // 解析第二个memreport文件
            string memReportPath2 = argParser.GetValue("-2f", null);
            // 解析差量分析的结果文件
            diffCSVPath = argParser.GetValue("-o", null);
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
        }

        public static string DoAnalyze(string[] args)
        {
            ArgParserLite argParser = new ArgParserLite(args);
            List<string> FileNameLst;
            List<string> FileFullPathLst;
            string diffCSVPath;
            ParseArgs(args, argParser, out FileNameLst, out FileFullPathLst, out diffCSVPath);

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

            DataManager dataMgr = new DataManager();
            // 解析并输出结果
            dataMgr.AnalyzeAndOutputResults(ref FileNameLst, ref FileFullPathLst, diffCSVPath);
            return diffCSVPath;
        }
    }
}
