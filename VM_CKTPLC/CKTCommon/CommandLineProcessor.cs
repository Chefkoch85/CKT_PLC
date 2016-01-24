using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CMD_MAP = System.Collections.Generic.Dictionary<string, string>;

namespace CKT.INPUT
{
    public class CommandLineProcessor
	{

		public enum eCmdArgs
		{
			/// <summary>
			/// input file to work on
			/// </summary>
			INF,
			/// <summary>
			/// PLC starts in "stop-mode"
			/// </summary>
			PST,
			/// <summary>
			/// PLC starts in "run-mode"
			/// </summary>
			PRU,
			/// <summary>
			/// PLC shares memory for other apps
			/// </summary>
			PSM,
		};

		static private readonly string[] CMD_KEYS = {
			"#INF:",	// file that should be worked on
			"#PST",		// PLC start in run mode
			"#PRU",		// PLC start in stop mode
			"#PSM",		// PLC should share its memory to another app
			"#CCP",		// compile complete Project
		};

		/// <summary>
		/// the cmd keys with params have to be the first in the list 
		/// and this int sets the how mucht there are
		/// </summary>
		static public readonly int MAX_CMD_WITH_PARAM = 1;

		CMD_MAP m_CmdList = null;

		public CMD_MAP CmdList
		{
			get
			{
				return m_CmdList;
			}
		}

		public bool readArgs(string[] args)
		{
			if (args.Length <= 0)
				return false;


			m_CmdList = new CMD_MAP();

			for(int nArg = 0; nArg < args.Length; nArg++)
			{
				for (int i = 0; i < CMD_KEYS.Length; i++)
				{
					if (args[nArg] == CMD_KEYS[i])
					{
						if (nArg < MAX_CMD_WITH_PARAM)
						{
							string argp1 = args[nArg + 1];
							if (argp1.StartsWith("\"") && argp1.EndsWith("\""))
								argp1 = argp1.Substring(1, argp1.Length - 2);

							m_CmdList.Add(args[nArg], args[++nArg]);
						}
						else
							m_CmdList.Add(args[nArg], "true");

					}
				}
			}

			if (m_CmdList.Count <= 0)
				return false;

			return true;
		}

		public bool hasArgs
		{
			get
			{
				return m_CmdList.Count > 0;
			}
		}

		public bool hasArg(eCmdArgs key)
		{
			return m_CmdList.Keys.Contains(CMD_KEYS[(int)key]);
		}
		public bool hasArg(string key)
		{
			return m_CmdList.Keys.Contains(key);
		}

		public string argAsString(string key)
		{
			if (m_CmdList.Keys.Contains(key))
				return m_CmdList[key];

			return null;
		}
		public bool argAsBool(string key)
		{
			if (m_CmdList.Keys.Contains(key))
			{
				bool val = false;
				if (Boolean.TryParse(m_CmdList[key], out val))
				{
					return val;
				}
			}

			return false;
		}
		public int argAsInt(string key)
		{
			if (m_CmdList.Keys.Contains(key))
			{
				int val = 0;
				if (Int32.TryParse(m_CmdList[key], out val))
				{
					return val;
				}
			}

			return 0;
		}
		public float argAsFloat(string key)
		{
			if (m_CmdList.Keys.Contains(key))
			{
				float val = 0.0f;
				if (Single.TryParse(m_CmdList[key], out val))
				{
					return val;
				}
			}

			return 0.0f;
		}

		public string argAsString(eCmdArgs key)
		{
			return argAsString(CMD_KEYS[(int)key]);
		}
		public bool argAsBool(eCmdArgs key)
		{
			return argAsBool(CMD_KEYS[(int)key]);
		}
		public int argAsInt(eCmdArgs key)
		{
			return argAsInt(CMD_KEYS[(int)key]);
		}
		public float argAsFloat(eCmdArgs key)
		{
			return argAsFloat(CMD_KEYS[(int)key]);
		}

	}
}
