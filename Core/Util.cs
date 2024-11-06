using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using System.Windows.Forms;

namespace Core
{
    public class Util
    {
        //public static string GetSelectedFile(string initDir = @"E:\")
        //{
        //    OpenFileDialog dialog = new OpenFileDialog();
        //    dialog.Multiselect = false;//该值确定是否可以选择多个文件
        //    dialog.Title = "请选择文件";
        //    dialog.Filter = "所有文件(*.memreport)|*.memreport";
        //    dialog.InitialDirectory = initDir;
        //    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        //    {
        //        return dialog.FileName;
        //    }
        //    return null;
        //}

        public static string RunCmd(string cmd)
        {
            cmd = cmd.Trim().TrimEnd('&') + " & exit";//说明：不管命令是否成功均执行exit命令，否则当调用ReadToEnd()方法时，会处于假死状态
            System.Diagnostics.Process p = new System.Diagnostics.Process();
            p.StartInfo.FileName = "cmd.exe";
            //p.StartInfo.WorkingDirectory = workingDir;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = true;   //接受来自调用程序的输入信息
            p.StartInfo.RedirectStandardOutput = true;  //由调用程序获取输出信息
            p.StartInfo.RedirectStandardError = true;   //重定向标准错误输出
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.StandardInput.WriteLine(cmd);
            p.StandardInput.AutoFlush = true;
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return output;
        }
    }
}
