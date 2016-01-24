using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Windows.Forms;
using System.IO;

using Keyboard = CKT.INPUT.Keyboard;
using CKT.VM.PLC_Basic;
using eCPUError = CKT.VM.PLC_Basic.CPUBasic.eCPUErrors;

using eBlockType = CKT.VM.PLC_Basic.VMPLCBasisc.eBlockType;

using COBFileHeader = CKT.VM.PLC_Basic.VMPLCBasisc.SCOBFileHeader;
using ProjectInfo = CKT.VM.PLC_Basic.VMPLCBasisc.SProjectInfo;
using BlockInfo = CKT.VM.PLC_Basic.VMPLCBasisc.SBlockInfo;
using VisuFileHeader = CKT.VM.PLC_Basic.VMPLCBasisc.SVisuFileHeader;
using VisuBlock = CKT.VM.PLC_Basic.VMPLCBasisc.SVisuBlock;

namespace CKT.VM.CKTPLC
{
    class PLCManager
    {
        /// <summary>
        /// 
        /// </summary>
        enum ePLCState
        {
            NONE,
            INIT,
            RUN,
            STOP,
            PAUSE,
            RESET,
            ERASE,
            ERROR,
        }

        enum eAppState
        {
            NONE,
            MENU,
            VISU,
        }

        /// <summary>
        /// max memory per region (IN/OUT/MARKER) (byte)
        /// </summary>
        static public readonly int MAX_PLC_MEMORY = 4096;

        /// <summary>
        /// max memory for instructions (byte)
        /// </summary>
        static public readonly int MAX_INSTR_MEMORY = 8192;

        /// <summary>
        /// instruction size
        /// </summary>
        static public readonly int INSTR_SIZE = 5;

        /// <summary>
        /// visu block size
        /// </summary>
        static public readonly int VISU_BLOCK_SIZE = 32;

        /// <summary>
        /// max memory for the stack (byte)
        /// </summary>
        static public readonly int MAX_STACK_MEMORY = 1024;


        static public readonly int FIELD_WIDTH = 16;    // data field width in chars
        static public readonly int FIELD_HIGHT = 3;     // data field height in rows (1=Name; 2=Seperator; 3=Value)
        static public readonly string FIELD_SEPERATOR = new String('-', 16);
        static public readonly int FIELD_SPACE = 1;     // 1 line or char as space between fields
        static public readonly int FIELD_MAX_ROW = 8;   // max 8 fields in one row

        byte[] m_byaInputMemory = null;
        byte[] m_byaOutputMemory = null;
        byte[] m_byaMarkerMemory = null;

        byte[] m_byaInstrMemory = null;
        byte[] m_byaStackMemory = null;

        ProjectInfo m_ProjectInfo;
        Dictionary<int, BlockInfo> m_BlockOverview = null;

        private List<VisuBlock> m_VisuBlocks = null;

        private PLCCPU m_PLCCPU = null;

        private ePLCState m_CurPLCState = ePLCState.NONE;
        private ePLCState m_OldPLCState = ePLCState.NONE;

        private eAppState m_CurAppState = eAppState.NONE;
        private eAppState m_OldAppState = eAppState.NONE;

        private bool m_bVisuLoaded = false;
        private bool m_bVisuRun = false;

        Keys[] m_InputKeys = { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8 };

        static private PLCManager m_Instance = null;
        static public PLCManager instance
        {
            get
            {
                if (m_Instance != null)
                    return m_Instance;

                return (m_Instance = new PLCManager());
            }
        }

        /// <summary>
        /// init PLC memory
        /// </summary>
        public void initPLC()
        {
            if (m_CurPLCState == ePLCState.NONE)
            {
                m_byaInputMemory = new byte[MAX_PLC_MEMORY];
                m_byaOutputMemory = new byte[MAX_PLC_MEMORY];
                m_byaMarkerMemory = new byte[MAX_PLC_MEMORY];

                m_byaStackMemory = new byte[MAX_STACK_MEMORY];

                Console.SetWindowSize(150, 50);

                m_CurPLCState = ePLCState.INIT;
                m_CurAppState = eAppState.MENU;    
            }
        }

        /// <summary>
        /// start the PLC Loop from begin
        /// </summary>
        public void startPLC()
        {
            if (m_CurPLCState != ePLCState.STOP)
                return;

            m_CurPLCState = ePLCState.RUN;
        }

        /// <summary>
        /// stop the PLC Loop and reset all to begin
        /// </summary>
        public void stopPLC()
        {
            if (m_CurPLCState != ePLCState.RUN)
                return;

            m_CurPLCState = ePLCState.STOP;

            m_PLCCPU.pause();
            m_PLCCPU.reset();
        }

        /// <summary>
        /// resume paused PLC from paused command
        /// </summary>
        public void resumePLC()
        {
            if (m_CurPLCState != ePLCState.PAUSE)
                return;

            m_CurPLCState = ePLCState.RUN;

            m_PLCCPU.pause();
        }

        /// <summary>
        /// pause the PLC Loop in the current command
        /// </summary>
        public void pausePLC()
        {
            if (m_CurPLCState != ePLCState.RUN)
                return;

            m_CurPLCState = ePLCState.PAUSE;

            m_PLCCPU.pause();
        }

        /// <summary>
        /// load a PLC Program in the Program-Memory
        /// </summary>
        public void loadPLC()
        {
            if (m_CurPLCState != ePLCState.STOP && m_CurPLCState != ePLCState.INIT)
                return;

            //m_CurPLCState = ePLCState.STOP;

            string msg = "";
            if (loadPLCProgram(ref m_byaInstrMemory, out msg))
            {
                Console.Clear();
                Console.WriteLine("ERROR with Object file:");
                Console.WriteLine(msg);

                Console.ReadKey();
                return;
            }
            
            if (m_CurPLCState == ePLCState.STOP)
            {
                Console.Clear();
                Console.WriteLine("Loading of project done!");
                Console.WriteLine(msg);

                Console.ReadKey();
                
                m_PLCCPU = new PLCCPU();
                m_PLCCPU.init(
                    ref m_byaInstrMemory,
                    ref m_byaStackMemory,
                    ref m_byaInputMemory,
                    ref m_byaOutputMemory,
                    ref m_byaMarkerMemory);
            }
        }

        /// <summary>
        /// reset PLC variables (IN/OUT/MARKER to 0) in the Variable-Memory 3x4096 byte
        /// </summary>
        public void resetPLC()
        {
            if (m_CurPLCState != ePLCState.PAUSE && m_CurPLCState != ePLCState.ERROR)
                return;

            m_CurPLCState = ePLCState.STOP;

            m_PLCCPU.pause();
            Array.Clear(m_byaInputMemory, 0, m_byaInputMemory.Length);
            Array.Clear(m_byaOutputMemory, 0, m_byaOutputMemory.Length);
            Array.Clear(m_byaMarkerMemory, 0, m_byaMarkerMemory.Length);
            m_PLCCPU.reset();
            m_PLCCPU.RunError = false;
        }

        /// <summary>
        /// erase PLC Program from Program-Memory
        /// </summary>
        public void erasePLC()
        {
            if (m_CurPLCState != ePLCState.STOP)
                return;

            m_CurPLCState = ePLCState.INIT;

            Array.Clear(m_byaInstrMemory, 0, m_byaInstrMemory.Length);
            Array.Clear(m_byaStackMemory, 0, m_byaStackMemory.Length);
        }

        /// <summary>
        /// runs the PLC Program (CPU is in run)
        /// </summary>
        public void runPLC()
        {
            if (m_CurPLCState != ePLCState.RUN)
                return;

            m_PLCCPU.run();

            if (m_PLCCPU.RunError)
            {
                m_CurPLCState = ePLCState.ERROR;
            }
        }


        // menu system
        enum eAbleTo
        {
            LOAD = 0,
            RUN = LOAD + 1,
            STOP = RUN + 1,
            PAUSE = STOP + 1,
            RESUME = PAUSE + 1,
            RESET = RESUME + 1,
            ERASE = RESET + 1,
            LOADVISU = ERASE + 1,
            SHOWVISU = LOADVISU + 1,
            SHOWBLOCK = SHOWVISU + 1,
            INFO = SHOWBLOCK + 1,
            EXIT = INFO + 1,
        };

        public bool runPLCMenu()
        {
            initPLC();

            int iChoice = 0;
            int minChoice = 0;
            int maxChoice = Enum.GetNames(typeof(eAbleTo)).Count() - 1;
            bool[] isAbleTo = new bool[maxChoice + 1];
            for (int i = 0; i < isAbleTo.Length; i++ )
                isAbleTo[i] = true;

            string[] sMenuEntrys = {
                                       "LOAD   PLC-PROGRAM",
                                       "RUN    PLC-PROGRAM",
                                       "STOP   PLC-PROGRAM",
                                       "PAUSE  PLC-PROGRAM",
                                       "RESUME PLC-PROGRAM",
                                       "RESET  PLC-PROGRAM",
                                       "ERASE  PLC-PROGRAM",
                                       "LOAD   PLC-VISU",
                                       "SHOW   PLC-DATA",
                                       "SHOW   PLC-BLOCKS",
                                       "INFO",
                                       "EXIT         [ESC]",
                                   };

            Keyboard.instance.registerKey(Keys.Escape);
            Keyboard.instance.registerKey(Keys.Enter);
            Keyboard.instance.registerKey(Keys.Up);
            Keyboard.instance.registerKey(Keys.Down);
            Keyboard.instance.registerKey(Keys.LShiftKey);

            foreach (Keys k in m_InputKeys)
                Keyboard.instance.registerKey(k);

            bool bRun = true;
            while (bRun)
            {
                Keyboard.instance.update();

                if (m_CurAppState != m_OldAppState)
                    Console.Clear();

                if (m_CurPLCState != m_OldPLCState)
                    Console.Clear();

                m_OldAppState = m_CurAppState;
                m_OldPLCState = m_CurPLCState;

                if (m_CurAppState == eAppState.MENU)
                {
                    AbleTo(ref isAbleTo);

                    Choice(ref iChoice, isAbleTo, minChoice, maxChoice);

                    DrawMenu(iChoice, isAbleTo, maxChoice, sMenuEntrys);
                }
                else if (m_CurAppState == eAppState.VISU)
                {
                    showVisu();
                }

                ProcessChoice(iChoice, ref bRun);
                
                runPLC();

                showPLCState();


                System.Threading.Thread.Sleep(100);
            }

            return false;
        }
        private void AbleTo(ref bool[] isAbleTo, bool all = false)
        {
            isAbleTo[(int)eAbleTo.LOAD] = m_CurPLCState == ePLCState.INIT || m_CurPLCState == ePLCState.STOP || all;
            isAbleTo[(int)eAbleTo.RUN] = m_CurPLCState == ePLCState.STOP || all;
            isAbleTo[(int)eAbleTo.STOP] = m_CurPLCState == ePLCState.RUN || all;
            isAbleTo[(int)eAbleTo.PAUSE] = m_CurPLCState == ePLCState.RUN || all;
            isAbleTo[(int)eAbleTo.RESUME] = m_CurPLCState == ePLCState.PAUSE || all;
            isAbleTo[(int)eAbleTo.RESET] = m_CurPLCState == ePLCState.PAUSE || m_CurPLCState == ePLCState.ERROR || all;
            isAbleTo[(int)eAbleTo.ERASE] = m_CurPLCState == ePLCState.STOP || all;
            isAbleTo[(int)eAbleTo.EXIT] = true || all;
            isAbleTo[(int)eAbleTo.LOADVISU] = m_CurPLCState == ePLCState.STOP || m_CurPLCState == ePLCState.INIT || m_CurPLCState == ePLCState.RUN || all;
            isAbleTo[(int)eAbleTo.SHOWVISU] = m_bVisuLoaded || all;
            isAbleTo[(int)eAbleTo.SHOWBLOCK] = m_CurPLCState == ePLCState.STOP || m_CurPLCState == ePLCState.PAUSE || all;
        }
        private void Choice(ref int choice,bool[] isAbleTo, int min, int max)
        {
            if (Keyboard.instance.isDown(Keys.Up))
            {
                choice--;

                
            }
            else if (Keyboard.instance.isDown(Keys.Down))
            {
                choice++;

                if (choice > max)
                    choice = min;

                while (!isAbleTo[choice])
                {
                    choice++;

                    if (choice > max)
                        choice = min;
                }
            }

            if (choice < min)
                choice = max;
            else if (choice > max)
                choice = min;

            while (!isAbleTo[choice])
            {
                choice--;

                if (choice < min)
                    choice = max;
            }
        }
        private void ProcessChoice(int choice, ref bool run)
        {
            // General keyboard actions
            if (Keyboard.instance.isUp(Keys.Escape))
            {
                if (m_CurAppState == eAppState.MENU)
                    run = false;
                else if (m_CurAppState == eAppState.VISU && m_bVisuRun)
                    m_bVisuRun = false;
            }

            // Visu keys for input (I0.0-I0.7)
            #region INPUT KEYS
            if (m_CurAppState == eAppState.VISU)
            {
                byte IB0 = m_byaInputMemory[0];

                byte[] IB0asBits = new byte[8];
                for (int i = 0; i < IB0asBits.Length; i++)
                    IB0asBits[i] = (byte)((IB0 & CPUBasic.BITVALUES[i]) >> (i));

                IB0 = 0;
                if (Keyboard.instance.isPress(Keys.LShiftKey))
                {
                    for (int k = 0; k < m_InputKeys.Length; k++)
                    {
                        if (Keyboard.instance.isDown(m_InputKeys[k]))
                        {
                            IB0asBits[k] = (byte)(1 - IB0asBits[k]);
                        }
                    }
                }
                else
                {
                    for (int k = 0; k < m_InputKeys.Length; k++)
                    {
                        if (Keyboard.instance.isDown(m_InputKeys[k]))
                        {
                            IB0asBits[k] = 1;
                        }
                        else if (Keyboard.instance.isUp(m_InputKeys[k]))
                        {
                            IB0asBits[k] = 0;
                        }
                    }
                }
                for (int i = 0; i < IB0asBits.Length; i++)
                    IB0 |= (byte)(IB0asBits[i] << (i));

                m_byaInputMemory[0] = IB0;
            }
            #endregion

            // only if we are in the main menu
            if (m_CurAppState != eAppState.MENU)
                return;

            #region MAIN MENU
            if (Keyboard.instance.isDown(Keys.Enter))
            {
                switch ((eAbleTo)choice)
                {
                    case eAbleTo.LOAD:
                        loadPLC();
                        break;

                    case eAbleTo.LOADVISU:
                        loadVisu();
                        break;

                    case eAbleTo.SHOWVISU:
                        m_bVisuRun = true;
                        m_CurAppState = eAppState.VISU;
                        break;

                    case eAbleTo.SHOWBLOCK:
                        showLoadedBlocks();
                        break;

                    case eAbleTo.STOP:
                        stopPLC();
                        break;

                    case eAbleTo.PAUSE:
                        pausePLC();
                        break;

                    case eAbleTo.RUN:
                        startPLC();
                        break;

                    case eAbleTo.RESUME:
                        resumePLC();
                        break;

                    case eAbleTo.RESET:
                        resetPLC();
                        break;

                    case eAbleTo.ERASE:
                        erasePLC();
                        break;

                    case eAbleTo.INFO:
                        showInfo();
                        break;

                    case eAbleTo.EXIT:
                        run = false;
                        break;
                }
            }
            #endregion
        }
        private void DrawMenu(int choice, bool[] isAbleTo, int maxChoice, string[] menuEntrys, string mark = "> ")
        {
            string nonMark = new String(' ', mark.Length);
            //Console.Clear();
            Console.SetCursorPosition(0, 0);
            Console.WriteLine(nonMark + "PLC MENU:");
            Console.WriteLine(nonMark + "------------------");

            for (int i = 0; i < maxChoice + 1; i++)
            {
                if (i == choice)
                    Console.Write(mark);
                else
                    Console.Write(nonMark);

                if (isAbleTo[i])
                    Console.WriteLine(menuEntrys[i]);
                else
                    Console.WriteLine();
            }
        }

        private int enumToInt(object enumName)
        {
            return (int)enumName;
        }


        // additonal menu functions
        private void showInfo()
        {
            // to get rid of the Enter-key in the console buffer
            while (Console.ReadLine().Length > 0)
                ;

            Console.Clear();
            Console.WriteLine("CKT-PLC Program Info");
            Console.WriteLine("--------------------");
            Console.WriteLine();
            Console.WriteLine("PRESS ESC TO CLOSE!");

            Console.ReadKey();
        }

        private void showLoadedBlocks()
        {
            // to get rid of the Enter-key in the console buffer
            while (Console.ReadLine().Length > 0)
                ;

            Console.Clear();
            Console.WriteLine("Loaded Blocks:\n-------------------------");

            int lengthName = 0, lengthSize = 0;
            foreach (BlockInfo bi in m_BlockOverview.Values)
            {
                if (bi.Name.Length > lengthName)
                    lengthName = bi.Name.Length;

                if (bi.Size.ToString().Length > lengthSize)
                    lengthSize = bi.Size.ToString().Length;
            }

            foreach (BlockInfo bi in m_BlockOverview.Values)
            {
                int spaces = lengthName - bi.Name.Length;
                string space = new String(' ', spaces);
                Console.Write("-> " + space);
                Console.Write(bi.Name);
                space = new string(' ', bi.isActive ? 1 : 0);
                Console.Write(" | " + space + bi.isActive + " | ");

                spaces = lengthSize - bi.Size.ToString().Length;
                space = new String(' ', spaces);
                Console.Write(space + bi.Size + " | ");
                Console.Write(bi.DateLastModify + " | ");
                Console.WriteLine(bi.NameAuthor);
            }

            Console.ReadKey();
        }

        private bool loadVisu()
        {
            // to get rid of the Enter-key in the console buffer
            while (Console.ReadLine().Length > 0)
                ;

            Console.Clear();

            FileInfo fi = new FileInfo(@"..\..\..\PLC_PROJ\PROJ_ONE\VISU.COV");
            if (!fi.Exists || fi.Length < 16)
            {
                Console.WriteLine("File not found or to small!\n" + fi.FullName);
                Console.ReadKey();
                return true;
            }
            FileStream fs = new FileStream(fi.FullName, FileMode.Open);
            BinaryReader br = new BinaryReader(fs);

            VisuFileHeader header = new VisuFileHeader();
            header.FileTag = new char[2];
            header.FileTag[0] = br.ReadChar();
            header.FileTag[1] = br.ReadChar();
            header.OffsetVisuData = br.ReadUInt16();
            header.VisuVersion = new Version(br.ReadByte(), br.ReadByte());
            header.Count = br.ReadInt16();
            header.CreationDate = new DateTime(br.ReadInt16(), br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte());
            br.ReadByte();   // read fill byte

            if (header.FileTag[0] != 'P' || header.FileTag[1] != 'V')
            {
                Console.WriteLine("File is no CKTPLC visu or is corupted!\n" + fi.Name);
                Console.ReadKey();
                return true;
            }

            List<char> stringList = new List<char>();
            while (br.PeekChar() != 0)
            {
                stringList.Add((char)br.ReadByte());
            }
            br.ReadChar();
            header.VisuCompany = new String(stringList.ToArray());

            stringList = new List<char>();
            while (br.PeekChar() != 0)
            {
                stringList.Add((char)br.ReadByte());
            }
            br.ReadChar();
            header.VisuAuthor = new String(stringList.ToArray());

            stringList = new List<char>();
            while (br.PeekChar() != 0)
            {
                stringList.Add((char)br.ReadByte());
            }
            br.ReadChar();
            header.VisuDescription = new String(stringList.ToArray());

            int size = header.Count * VISU_BLOCK_SIZE;
            if (size < fi.Length - header.OffsetVisuData || size > fi.Length - header.OffsetVisuData)
            {
                Console.WriteLine("File size is to small or large for the count of visu blocks!\n"
                    + fi.Name + " size: " + fi.Length + " | header: " + header.OffsetVisuData + 
                    "\nsize all visu blocks: " + (fi.Length - header.OffsetVisuData) + " | should: " + size);
                Console.ReadKey();
                return true;
            }

            m_VisuBlocks = new List<VisuBlock>(header.Count);

            br.BaseStream.Seek(header.OffsetVisuData, SeekOrigin.Begin);
            int i = 0;
            while (br.PeekChar() != -1)
            {
                VisuBlock vb = new VisuBlock();
                vb.Index = br.ReadInt16();
                vb.Row = br.ReadByte();
                vb.Column = br.ReadByte();
                vb.MemLocation = (uint)(br.ReadByte() << 24);
                vb.MemLocation |= (uint)(br.ReadByte() << 16);
                vb.MemLocation |= (uint)(br.ReadByte() << 8);
                vb.MemLocation |= (uint)(br.ReadByte());
                vb.Flags = br.ReadByte();

                // read the fill bytes
                br.ReadBytes(7);

                char[] name = new char[16];
                byte[] nameAsBytes = br.ReadBytes(16);
                Array.Copy(nameAsBytes, name, 16);
                vb.Name = new String(name);
                vb.Name = vb.Name.Substring(0, vb.Name.IndexOf('\0'));

                m_VisuBlocks.Add(vb);
                i++;
            }

            if (m_VisuBlocks.Count != header.Count)
            {
                Console.WriteLine("File contains wrong number of visu blocks!\n"
                    + fi.Name + " should: " + header.Count + " | has: " + m_VisuBlocks.Count);
                Console.ReadKey();
                return true;
            }

            m_bVisuLoaded = true;
            return false;
        }
        private void showVisu()
        {
            if (!m_bVisuLoaded || m_CurPLCState != ePLCState.RUN && false)
                return;
            
            if (m_CurAppState != eAppState.VISU || !m_bVisuRun)
            {
                m_CurAppState = eAppState.MENU;
                return;
            }

            //Console.Clear();
            Console.SetCursorPosition(0, 0);
            string tmp = "";

            int oldRow = 0, rowSpace = 0, colSpace = 0;
            foreach (VisuBlock vb in m_VisuBlocks)
            {
                int row = vb.Row * FIELD_HIGHT;
                int col = vb.Column * FIELD_WIDTH;

                if (row != 0)
                {
                    if (row != oldRow)
                        rowSpace++;

                    row += rowSpace;
                    oldRow = row;
                }
                
                if (col != 0)
                {
                    colSpace++;
                    col += colSpace;
                }
                else
                {
                    colSpace = 0;
                }

                tmp = vb.Name.PadRight(FIELD_WIDTH, ' ');
                Console.SetCursorPosition(col, row);
                Console.BackgroundColor = ConsoleColor.Blue;
                Console.Write(tmp);
                Console.SetCursorPosition(col, row + 1);
                Console.Write(FIELD_SEPERATOR);
                Console.SetCursorPosition(col, row + 2);
                if (vb.Flags == 0x80)
                {
                    tmp = "";
                    byte[] result = getValue(vb.MemLocation, true);
                    foreach (byte b in result)
                        tmp += b + " ";
                }
                else
                {
                    tmp = getValue(vb.MemLocation).ToString().PadRight(FIELD_WIDTH, ' ');
                }
                Console.Write(tmp);
                Console.BackgroundColor = ConsoleColor.Black;

            }
        }
        private byte[] getValue(uint target, bool eightBit)
        {
            byte[] result = new byte[8];

            uint len = (CPUBasic.FLAG_BITFIELD_VAL & target) >> 20;
            if (len != 1)
                return null;

            byte memVal = (byte)getValue(target);

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (byte)((memVal & CPUBasic.BITVALUES[i]) >> (i));
            }

            return result;
        }
        private int getValue(uint target)
        {
            bool isMemLocation = (CPUBasic.FLAG_MEM_OR_NUM & target) > 0;
            bool isJump = (CPUBasic.FLAG_JUMP_ADDRESS & target) > 0;
            bool isBitAccess = (CPUBasic.FLAG_BIT_ACCESS & target) > 0;
            uint len = (CPUBasic.FLAG_BITFIELD_VAL & target) >> 20;
            CPUBasic.eMemRange witchMem = (CPUBasic.FLAG_MEM_MARKER & target) > 0 ? CPUBasic.eMemRange.MARKER : CPUBasic.eMemRange.NONE;
            witchMem = (CPUBasic.FLAG_MEM_OUTPUT & target) > 0 ? CPUBasic.eMemRange.OUTPUT : witchMem;
            witchMem = (CPUBasic.FLAG_MEM_INPUT & target) > 0 ? CPUBasic.eMemRange.INPUT : witchMem;

            int value = 0;
            if (isMemLocation)
                value = (int)(CPUBasic.FLAG_MEMADR & target);
            else
                value = (int)target;

            int memValue = getValueMem(isBitAccess, witchMem, value, (int)len);

            return memValue;
        }
        private int getValueMem(bool isBit, CPUBasic.eMemRange range, int targetVal, int bitfield)
        {
            int akkuVal = 0;
            switch (range)
            {
                case CPUBasic.eMemRange.MARKER:
                    if (isBit)
                    {
                        akkuVal = m_byaMarkerMemory[targetVal] & CPUBasic.BITVALUES[bitfield];
                    }
                    else
                    {
                        for (int i = 0; i < bitfield; i++)
                        {
                            akkuVal |= m_byaMarkerMemory[targetVal + i] << (i * 8);
                        }
                    }
                    break;

                case CPUBasic.eMemRange.OUTPUT:
                    if (isBit)
                    {
                        akkuVal = m_byaOutputMemory[targetVal] & CPUBasic.BITVALUES[bitfield];
                    }
                    else
                    {
                        for (int i = 0; i < bitfield; i++)
                        {
                            akkuVal |= m_byaOutputMemory[targetVal + i] << (i * 8);
                        }
                    }
                    break;

                case CPUBasic.eMemRange.INPUT:
                    if (isBit)
                    {
                        akkuVal = m_byaInputMemory[targetVal] & CPUBasic.BITVALUES[bitfield];
                    }
                    else
                    {
                        for (int i = 0; i < bitfield; i++)
                        {
                            akkuVal |= m_byaInputMemory[targetVal + i] << (i * 8);
                        }
                    }
                    break;
            }

            return akkuVal;
        }

        // show general infos for the PLC
        private void showPLCState()
        {
            Console.SetCursorPosition(0, Console.WindowHeight - 2);
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Black;

            double time = 0.0;
            if (m_PLCCPU != null)
                time = m_PLCCPU.CycleTime.Ticks / (double)TimeSpan.TicksPerMillisecond;

            Console.Write(" APP-STATE: " + m_CurAppState + " \x01C0 CPU-STATE: " + String.Format("{0,-5}", m_CurPLCState) + " \x01C0 CYCLE: " + String.Format("{0,5:###.#}", time) + "ms ");

            if (m_PLCCPU != null && m_PLCCPU.RunError)
            {
                Console.SetCursorPosition(Console.WindowWidth - 50, Console.WindowHeight - 2);
                Console.Write(" CPU-ERROR: " + String.Format("{0,10}:{1, -27} ",m_PLCCPU.CurrentErrorInstr, m_PLCCPU.CurrentError));
            }

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.BackgroundColor = ConsoleColor.Black;
        }

        // load a Project file and read in all data
        private bool loadPLCProgram(ref byte[] instrMem, out string msg)
        {
            // to get rid of the Enter-key in the console buffer
            while (Console.ReadLine().Length > 0)
                ;

            msg = "SUCCESS: Object file loaded!";
            FileInfo fi = null;
            List<byte> instrList = null;
            bool fileOK = false, readOK = false;
            while (!fileOK && !readOK)
            {
                Console.Clear();
                Console.WriteLine("PLC PROJECT FILE or Enter leave:");
                string file = Console.ReadLine();

                if (file.Length <= 0)
                {
                    return false;
                }

                fi = new FileInfo(@"..\..\..\PLC_PROJ\PROJ_ONE\" + file + ".cop");
                if (!fi.Exists || fi.Length < 23)    // 23 for the moment normal lenght data + zero's for the strings
                {
                    Console.WriteLine("File not found or to small:\n" + fi.FullName + "\nPlease check file and try again!\nPRESS ANY KEY TO PROCCED");
                    Console.ReadKey();
                    continue;
                }

                fileOK = true;
                string path = fi.DirectoryName;

                if (readProjHeader(fi.FullName, ref msg))
                {
                    return true;
                }

                if (readProjEntry(fi.FullName, ref msg))
                {
                    return true;
                }

                int size = 0;
                foreach (BlockInfo bi in m_BlockOverview.Values)
                    size += (int)bi.Size;

                instrList = new List<byte>(size);

                // read Block data
                for (int i = 0; i < m_BlockOverview.Count; i++)
                {
                    if (readBlocks(i, path, m_BlockOverview[i].Name, ref msg, ref instrList))
                    {
                        return true;
                    }
                }
            }

            instrMem = instrList.ToArray();

            msg = "SUCCESS:\nProject file: " + fi.Name + " loaded!\n" + 
                "SIZE: " + instrList.Count() + " | INSTR: " + (instrList.Count / INSTR_SIZE);

            m_CurPLCState = ePLCState.STOP;

            return false;
        }
        private bool readProjHeader(string fileName, ref string msg)
        {
            // read Project header data
            FileStream fs = new FileStream(fileName, FileMode.Open);
            BinaryReader br = new BinaryReader(fs);

            m_ProjectInfo = new ProjectInfo();
            m_ProjectInfo.FileTag = new char[2];
            m_ProjectInfo.FileTag[0] = br.ReadChar();
            m_ProjectInfo.FileTag[1] = br.ReadChar();

            if (m_ProjectInfo.FileTag[0] != 'P' || m_ProjectInfo.FileTag[1] != 'R')
            {
                br.Close();
                fs.Close();
                msg = "File is no CKT-PLC Project Object file or is corupted!\n-> " + fileName;
                return true;
            }

            m_ProjectInfo.OffsetBlockData = br.ReadInt16();
            m_ProjectInfo.ProjectVersion = new Version(br.ReadByte(), br.ReadByte());
            m_ProjectInfo.ObjectCount = br.ReadByte();
            m_ProjectInfo.DateCreation = new DateTime(br.ReadUInt16(), br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte());
            m_ProjectInfo.DateLastModify = new DateTime(br.ReadUInt16(), br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte());

            List<char> stringList = new List<char>();
            while (br.PeekChar() != 0)
            {
                stringList.Add((char)br.ReadByte());
            }
            br.ReadChar();
            m_ProjectInfo.ProjectName = new String(stringList.ToArray());

            stringList = new List<char>();
            while (br.PeekChar() != 0)
            {
                stringList.Add((char)br.ReadByte());
            }
            br.ReadChar();
            m_ProjectInfo.ProjectCompany = new String(stringList.ToArray());

            stringList = new List<char>();
            while (br.PeekChar() != 0)
            {
                stringList.Add((char)br.ReadByte());
            }
            br.ReadChar();
            m_ProjectInfo.ProjectAuthor = new String(stringList.ToArray());

            stringList = new List<char>();
            while (br.PeekChar() != 0)
            {
                stringList.Add((char)br.ReadByte());
            }
            br.ReadChar();
            m_ProjectInfo.ProjectDesc = new String(stringList.ToArray());
            
            br.Close();
            fs.Close();

            return false;
        }
        private bool readProjEntry(string fileName, ref string msg)
        {
            // read Project header data
            FileStream fs = new FileStream(fileName, FileMode.Open);
            BinaryReader br = new BinaryReader(fs);

            m_BlockOverview = new Dictionary<int, BlockInfo>(m_ProjectInfo.ObjectCount);

            br.BaseStream.Seek(m_ProjectInfo.OffsetBlockData, SeekOrigin.Begin);
            long p = br.BaseStream.Position;
            for (int i = 0; i < m_ProjectInfo.ObjectCount; i++)
            {
                BlockInfo bi = new BlockInfo();
                bi.BlockNumber = br.ReadByte();
                bi.BlockType = (eBlockType)br.ReadByte();
                bi.isActive = br.ReadBoolean();
                bi.Size = (uint)br.ReadInt32();
                bi.InstrCount = (uint)br.ReadInt32();
                bi.UpdateNumber = (uint)br.ReadInt32();
                bi.DateCreation = new DateTime(br.ReadUInt16(), br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte());
                bi.DateLastModify = new DateTime(br.ReadUInt16(), br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte());
                bi.FileVersion = new Version(br.ReadByte(), br.ReadByte());

                List<char> stringList = new List<char>();
                while (br.PeekChar() != 0)
                {
                    stringList.Add((char)br.ReadByte());
                }
                br.ReadChar();
                bi.Name = new String(stringList.ToArray());

                stringList = new List<char>();
                while (br.PeekChar() != 0)
                {
                    stringList.Add((char)br.ReadByte());
                }
                br.ReadChar();
                bi.NameCompany = new String(stringList.ToArray());

                stringList = new List<char>();
                while (br.PeekChar() != 0)
                {
                    stringList.Add((char)br.ReadByte());
                }
                br.ReadChar();
                bi.NameAuthor = new String(stringList.ToArray());

                //check error case file is to early at the end
                int c = br.PeekChar();
                if (c == -1 && i != m_ProjectInfo.ObjectCount - 1)
                {
                    msg = "Unexpected file end\n-> " + fileName;
                    return true;
                }

                m_BlockOverview.Add(i, bi);
            }

            br.Close();
            fs.Close();

            return false;
        }
        private bool readBlocks(int i, string path, string fileName, ref string msg, ref List<byte> instrList)
        {
            FileInfo fi = new FileInfo(Path.Combine(path, fileName));
            if (!fi.Exists || fi.Length < 26)    // file size min 26 byte (file header + 2 instructions)
            {
                msg = "File not found or to small:\n-> " + fi.FullName;
                return true;
            }

            FileStream fs = new FileStream(fi.FullName, FileMode.Open);
            BinaryReader br = new BinaryReader(fs);

            COBFileHeader header = new COBFileHeader(
                    br.ReadByte(), br.ReadByte(),
                    br.ReadByte(), br.ReadBoolean(),
                    br.ReadUInt32(), br.ReadUInt32(),
                    br.ReadByte(),
                    br.ReadChar(), br.ReadChar());

            br.ReadByte();  // read reserved byte

            if (header.FileTag[0] != 'O' || header.FileTag[1] != 'B')
            {
                br.Close();
                fs.Close();
                msg = "File is no CKT-PLC Object file or is corupted!\n-> " + fi.Name;
                return true;
            }
            else if (header.CompilerVersion > PLCCPU.CPU_VERSION)
            {
                br.Close();
                fs.Close();
                msg = "The Compiler version is not supported from this CPU!\nFILE: " + header.CompilerVersion.ToString() + " | CPU: " + PLCCPU.CPU_VERSION;
                return true;
            }
            else if (header.BlockType != eBlockType.PB)  //TODO: add the other Blocktypes when ready
            {
                br.Close();
                fs.Close();
                msg = "File has a not supported Block type!\n-> " + fi.Name;
                return true;
            }
            else if (header.InstrCount < 2)
            {
                br.Close();
                fs.Close();
                msg = "File has not enough instructions!\nCOUNT = " + header.InstrCount + "\n-> " + fi.Name;
                return true;
            }
        
            BlockInfo tmp = m_BlockOverview[i];
            tmp.CompilerVersion = header.CompilerVersion;
            m_BlockOverview.Remove(i);
            m_BlockOverview.Add(i, tmp);

            if (m_BlockOverview[i].isActive && header.isActive)
            {
                msg += "\nNAME: " + fi.Name.ToUpper();
                msg += "\nSIZE: " + fi.Length;
                msg += " | INSTR: " + header.InstrCount;
                msg += "\nDATE: " + fi.LastWriteTime;

                List<byte> BlockInstrList = new List<byte>((int)(header.InstrCount * INSTR_SIZE));
                int j = 0;
                while (br.PeekChar() != -1)
                {
                    BlockInstrList.Add(br.ReadByte());
                    if (BlockInstrList[j] == 0)
                    {
                        msg = "No CPU command found, File corupted!\n-> " + fi.Name;
                        return true;
                    }

                    BlockInstrList.Add(br.ReadByte());
                    BlockInstrList.Add(br.ReadByte());
                    BlockInstrList.Add(br.ReadByte());
                    BlockInstrList.Add(br.ReadByte());

                    j += INSTR_SIZE;
                }

                br.Close();
                fs.Close();

                if (BlockInstrList.Count != header.InstrCount * INSTR_SIZE)
                {
                    msg = "Instruction count from file not correct!\nFILE SHOULD: " + header.InstrCount +
                                                                  "\nFILE HAS:    " + (BlockInstrList.Count / INSTR_SIZE) +
                                                                  "\n-> " + fi.Name;
                    return true;
                }

                foreach (byte b in BlockInstrList)
                    instrList.Add(b);
            }
            else
            {
                msg += "\nFile is not active!";
                msg += "\nNAME: " + fi.Name.ToUpper();
                msg += "\nSIZE: " + fi.Length;
                msg += " | INSTR: " + header.InstrCount;
                msg += "\nDATE: " + fi.LastWriteTime;
            }

            Console.Clear();
            Console.WriteLine("Object file loaded:");
            Console.WriteLine(msg);

            Console.ReadKey();

            return false;
        }
    }
}
