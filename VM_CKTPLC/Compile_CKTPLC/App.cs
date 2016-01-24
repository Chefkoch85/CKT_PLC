using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using CKT.INPUT;
using eCmdArgs = CKT.INPUT.CommandLineProcessor.eCmdArgs;

using CKT.VM.PLC_Basic;

namespace CKT.VM.COMPILE
{
    class App
    {
		public enum eValidExtensions
		{
			ED,
			DB,
			FB,
			PR,
			PV,
			EV,
			NONE,
		};

		static public readonly string[] VALID_INPUTS =
		{
			".CED",  // Program Block (editable)
			".CDB",  // Data Block (editable)
			".CFB",  // Function Block (editable)
			".CPR",  // Project file (editable)
			".CVP",  // PLC Visu (editable)
			".CEV",	// PLC Editor Visu (editable)
		};

		static public readonly string[] VALID_OUTPUTS =
		{
			".COB",
			".COB",
			".COB",
			".COP",
			".COV",
			".COV",
		};

        static App m_Instance = null;
        static public App instance
        {
            get
            {
                if (m_Instance != null)
                    return m_Instance;

                return (m_Instance = new App());
            }
        }

		CommandLineProcessor m_Cmd = null;

		static public readonly Version APP_VERSION = new Version(0, 2);

		List<string> m_FileData = null;

		eValidExtensions m_iExtensionIndex = eValidExtensions.NONE;
		bool m_bFileLoaded = false;

		public void CmdLineArgs(string[] args)
        {
			m_Cmd = new CommandLineProcessor();
			m_Cmd.readArgs(args);
        }

        public void run()
        {
			// automated compile with args
			if(m_Cmd != null && m_Cmd.hasArgs && m_Cmd.hasArg(eCmdArgs.INF))
			{
				autoCompile(m_Cmd.argAsString(eCmdArgs.INF));
				return;
			}

			// manuell compile with user input by keyboard
			manuellCompile();

        }

		private void autoCompile(string file = "")
		{
			if(file != String.Empty)
			{
				if(readFile(file))
				{
					return;
				}
			}

			if(!m_bFileLoaded)
			{
				Console.Clear();
				Console.WriteLine("-> CKT-PLC-Compile <-");
				Console.WriteLine("-- ERROR ------------");
				Console.WriteLine("No file loaded! Press any key!");
				Console.ReadKey();
			}
			
			bool result = false;
			int errLine = -1;
			string msg = "";
			ICompile Compiler = null;
			switch(m_iExtensionIndex)
			{
				case eValidExtensions.ED:
					Compiler = new CompilerCED();
					result = Compiler.loadData(@"..\..\..\RES\COMPILER\CMD_VERSIONS.CRD", out msg);
					break;
					
				case eValidExtensions.DB:
					Compiler = new CompilerCDB();
					break;
					
				case eValidExtensions.FB:
					Compiler = new CompilerCFB();
					break;

				case eValidExtensions.PR:
					Compiler = new CompilerCPR();
					break;

				case eValidExtensions.PV:
					Compiler = new CompilerCPV();
					break;

				case eValidExtensions.EV:
					Compiler = new CompilerCEV();
					break;
			}


			result = Compiler.compile(m_FileData, out errLine, out msg);

			if (result)
			{
				Console.Clear();
				Console.WriteLine("-> CKT-PLC-Compile <-");
				Console.WriteLine("-- ERROR ------------");
				Console.WriteLine(msg);
				Console.WriteLine("LINE: " + errLine);
				Console.WriteLine("Press any key!");
				Console.ReadKey();
				return;
			}

			result = Compiler.write(out msg);

			Console.Clear();
			Console.WriteLine("-> CKT-PLC-Compile <-");
			Console.WriteLine("-- COMPLETE----------");
			Console.WriteLine(msg);
			Console.WriteLine("Press any key!");
			Console.ReadKey();

		}

		private void manuellCompile()
		{
			char pressKey = '\0';
			while (pressKey != 'e' && pressKey != 'E' && pressKey != 0x1b)
			{
				Console.Clear();
				Console.WriteLine("-> CKT-PLC-Compile <-");
				Console.WriteLine("--- MENU ------------");
				Console.WriteLine("    FILE    [F]");
				if (m_bFileLoaded)
					Console.WriteLine("    COMPILE [C]");
				else
					Console.WriteLine();
				Console.WriteLine("    EXIT    [E]");
				Console.Write("    CHOICE : ");

				pressKey = Console.ReadKey().KeyChar;
				
				if (pressKey == 'F' || pressKey == 'f')
				{
					string file = @"..\..\..\PLC_PROJ\PROJ_ONE\";
					Console.Clear();
					Console.WriteLine("-> CKT-PLC-Compile <-");
					Console.WriteLine("--- LOAD FILE -------");
					Console.WriteLine("    FILE:");
					file += Console.ReadLine();

					if (readFile(file))
					{
						continue;
					}
				}
				else if(m_bFileLoaded && (pressKey == 'C' || pressKey == 'c'))
				{
					autoCompile();
				}

			}

		}

		private bool readFile(string file)
		{
			if(file == String.Empty)
			{
				Console.WriteLine("-> No file given! Press any key.");
				Console.ReadKey();
				return true;
			}

			FileInfo fi = new FileInfo(file);
			if(!fi.Exists || fi.Length <= 0)
			{
				Console.WriteLine("-> File not found or to small! Press any key.");
				Console.ReadKey();
				return true;
			}

			for (int i = 0; i < VALID_INPUTS.Length; i++)
			{
				if (fi.Extension.ToUpper() == VALID_INPUTS[i])
				{
					m_iExtensionIndex = (eValidExtensions)i;
					file = fi.FullName;
					StreamReader tr = new StreamReader(file);

					m_FileData = new List<string>();

					string line = "";
					while((line = tr.ReadLine()) != null)
					{
						line = line.Trim();
						
						m_FileData.Add(line);
					}

					tr.Close();
				}
			}

			if(m_FileData.Count <= 0)
			{
				Console.WriteLine("-> No CKT-PLC code lines found! Press any key.");
				Console.ReadKey();
				return true;
			}

			m_bFileLoaded = true;
			return false;
		}

	}
}
