using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemDiffApp
{
    public class Globals
    {
        static string CFG_FULL_PATH = "";
        static string CFG_NAME = "Config.ini";
        static Core.Config cfg;

        public static string GLastSelectedFolder
        {
            get;set;
        }
        public static string GLastResultOutpoutFolder
        {
            get;set;
        }
        public static string GCfgFoler
        {
            get; set;
        }

        public static void Init()
        {
            string curDir = Directory.GetCurrentDirectory();
            string cfgPath = curDir + "/" + CFG_NAME;
            bool findCfg = File.Exists(cfgPath);
            while (findCfg == false)
            {
                DirectoryInfo cd = new DirectoryInfo(curDir);
                if (cd.Parent == null)
                    break;
                curDir = cd.Parent.FullName;
                cfgPath = curDir + "/" + CFG_NAME;
                findCfg = File.Exists(cfgPath);
            }
            
            if(findCfg)
            {
                cfg = new Core.Config(cfgPath);
                CFG_FULL_PATH = cfgPath;
                GCfgFoler = (new FileInfo(cfgPath)).Directory.FullName;
            }
            ReadCfg();
        }

        public static void Destroy()
        {
            SaveCfg();
        }

        public static void ReadCfg()
        {
            if (cfg!=null)
            {
                GLastSelectedFolder = cfg.get("LastSelectedFolder", "");
                GLastResultOutpoutFolder = cfg.get("LastResultOutpoutFolder", "");
            }
        }

        public static void SaveCfg()
        {
            if (cfg != null)
            {
                cfg.set("LastSelectedFolder", GLastSelectedFolder);
                cfg.set("LastResultOutpoutFolder", GLastResultOutpoutFolder);
                cfg.save();
            }
        }

        public static string GetSelectedFile(string initDir = @"E:\")
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Multiselect = false;//该值确定是否可以选择多个文件
            dialog.Title = "请选择文件";
            dialog.Filter = "所有文件(*.memreport)|*.memreport";
            dialog.InitialDirectory = initDir;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return dialog.FileName;
            }
            return null;
        }
    }
}
