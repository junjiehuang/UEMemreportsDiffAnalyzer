using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using System.Windows.Forms;
using System.IO;
using System.Collections;

namespace Core
{
    public class Config
    {
        public Dictionary<string, string> configData;
        string fullFileName;

        public Config(string _fileFullPathName)//注意路径
        {
            configData = new Dictionary<string, string>();
            //fullFileName = Application.StartupPath + @"\" + _fileName;
            //bool hasCfgFile = File.Exists(Application.StartupPath + @"\" + _fileName);
            //if (hasCfgFile == false)
            //{
            //    StreamWriter writer = new StreamWriter(File.Create(Application.StartupPath + @"\" + _fileName), Encoding.Default);
            //    writer.Close();
            //}
            //StreamReader reader = new StreamReader(Application.StartupPath + @"\" + _fileName, Encoding.Default);
            fullFileName = _fileFullPathName;
            bool hasCfgFile = File.Exists(fullFileName);
            if (hasCfgFile == false)
            {
                hasCfgFile = false;
                StreamWriter writer = new StreamWriter(File.Create(fullFileName), Encoding.Default);
                writer.Close();
            }
            StreamReader reader = new StreamReader(fullFileName, Encoding.Default);
            string line;
            int indx = 0;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith(";") || string.IsNullOrEmpty(line))
                    configData.Add(";" + indx++, line);
                else
                {
                    string[] key_value = line.Split('=');
                    if (key_value.Length >= 2)
                        configData.Add(key_value[0], key_value[1]);
                    else
                        configData.Add(";" + indx++, line);
                }
            }
            reader.Close();
        }

        public string get(string key, string defaultValue = "")
        {
            if (configData.Count <= 0)
                return defaultValue;
            else if (configData.ContainsKey(key))
                return configData[key].ToString();
            else
                return defaultValue;
        }

        public void set(string key, string value)
        {
            if (configData.ContainsKey(key))
                configData[key] = value;
            else
                configData.Add(key, value);
        }

        public void save()
        {
            StreamWriter writer = new StreamWriter(fullFileName, false, Encoding.Default);
            IDictionaryEnumerator enu = configData.GetEnumerator();
            while (enu.MoveNext())
            {
                if (enu.Key.ToString().StartsWith(";"))
                    writer.WriteLine(enu.Value);
                else
                    writer.WriteLine(enu.Key + "=" + enu.Value);
            }
            writer.Close();
        }
    }
}