using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    class ArgParserLite
    {
        string[] m_Args;

        public ArgParserLite(string[] args)
        {
            m_Args = args;
        }

        public bool HasOption(string option)
        {
            bool result = false;
            for (int iArg = 0; iArg < m_Args.Length; ++iArg)
            {
                string arg = m_Args[iArg].Trim();
                if (arg == option)
                {
                    result = true;
                    break;
                }
            }
            return result;
        }

        public string GetValue(string option, string defaultValue = "")
        {
            string result = defaultValue;
            for (int iArg = 0; iArg < m_Args.Length; ++iArg)
            {
                string arg = m_Args[iArg].Trim();
                if (arg == option && m_Args.Length > iArg + 1)
                {
                    result = m_Args[iArg + 1].Trim();
                    break;
                }
            }
            return result;
        }

        public bool GetOption(string option, bool defaultValue = false)
        {
            bool result = defaultValue;
            for (int iArg = 0; iArg < m_Args.Length; ++iArg)
            {
                string arg = m_Args[iArg].Trim();
                if (arg == option)
                {
                    result = true;
                    break;
                }
            }
            return result;
        }
    }

}
