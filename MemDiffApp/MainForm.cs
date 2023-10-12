using MemReportParser;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MemDiffApp
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void button1_selfile_Click(object sender, EventArgs e)
        {
            this.textBox_file1.Text = Core.Util.GetSelectedFile(Globals.GLastSelectedFolder);
            if (string.IsNullOrEmpty(this.textBox_file1.Text) == false)
            {
                FileInfo fi = new FileInfo(this.textBox_file1.Text);
                Globals.GLastSelectedFolder = fi.Directory.FullName;
            }
        }

        private void button2_selfile_Click(object sender, EventArgs e)
        {
            this.textBox_file2.Text = Core.Util.GetSelectedFile(Globals.GLastSelectedFolder);
            if (string.IsNullOrEmpty(this.textBox_file2.Text) == false)
            {
                FileInfo fi = new FileInfo(this.textBox_file2.Text);
                Globals.GLastSelectedFolder = fi.Directory.FullName;
            }
        }

        private void button_analyze_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(this.textBox_file1.Text) && string.IsNullOrEmpty(this.textBox_file2.Text))
                return;

            //string cmd = "E:\\Projs\\UEMemreportsDiffAnalyzer\\Bin\\MemDiffCmd.exe ";
            //if(string.IsNullOrEmpty(this.textBox_file1.Text) == false)
            //{
            //    cmd += "-1f " + this.textBox_file1.Text;
            //    if(string.IsNullOrEmpty(this.textBox_file2.Text) == false)
            //    {
            //        cmd += " -2f " + this.textBox_file2.Text;
            //    }
            //}
            //else if (string.IsNullOrEmpty(this.textBox_file2.Text) == false)
            //{
            //    cmd += " -1f " + this.textBox_file2.Text;
            //}
            //cmd += " -o E:\\Projs\\UEMemreportsDiffAnalyzer\\MemReports\\";
            //string ret = Core.Util.RunCmd(cmd);
            //Debug.Write("ExecCmdRet : " + ret);

            string RetOutFolder = "E:\\Projs\\UEMemreportsDiffAnalyzer\\MemReports\\";
            if(string.IsNullOrEmpty(Globals.GLastResultOutpoutFolder) == false)
            {
                RetOutFolder = Globals.GLastResultOutpoutFolder;
            }
            List<string> cmdargs = new List<string>();
            cmdargs.Add(Globals.GCfgFoler + "\\Bin\\MemDiffCmd.exe");
            if (string.IsNullOrEmpty(this.textBox_file1.Text) == false)
            {
                cmdargs.Add("-1f");
                cmdargs.Add(this.textBox_file1.Text);
                if (string.IsNullOrEmpty(this.textBox_file2.Text) == false)
                {
                    cmdargs.Add("-2f");
                    cmdargs.Add(this.textBox_file2.Text);
                }
            }
            else if(string.IsNullOrEmpty(this.textBox_file2.Text) == false)
            {
                cmdargs.Add("-1f");
                cmdargs.Add(this.textBox_file2.Text);
            }
            cmdargs.Add("-0");
            cmdargs.Add(RetOutFolder);            
            string reportfile = Analyzer.DoAnalyze(cmdargs.ToArray());

            if(reportfile != null)
            {
                //System.Diagnostics.Process.Start("explorer.exe", reportfile);
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = @"C:\\Program Files\\Microsoft Office\\root\\Office16\\EXCEL.exe";
                startInfo.Arguments = reportfile;
                Process.Start(startInfo);
            }
        }
    }
}