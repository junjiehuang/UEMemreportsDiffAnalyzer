#define ENABLE_SELF_TEST

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace MemReportParser
{

    class Program
    {


        static void Main(string[] args)
        {
            // self test commands
#if ENABLE_SELF_TEST
            if (args == null || args.Length == 0)
            {
                string CurPath = System.IO.Directory.GetCurrentDirectory() + "/../../../..";
                args = new[]
                {
                    "-1f",
                    string.Format("{0}/MemReports/1-base/h_fighting.memreport", CurPath),
                    //"-2f",
                    //string.Format("{0}/MemReports/Envi_Umeda-Android-27-14.22.19.memreport", CurPath),
                    "-o",
                    string.Format("{0}/MemReports", CurPath),
                    "-p",
                    "xls",
                };
            }
#endif
            Core.Analyzer.DoAnalyze(args);
        }
    }
}
