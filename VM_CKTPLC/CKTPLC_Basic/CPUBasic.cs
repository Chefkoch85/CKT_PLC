using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CKT.VM.PLC_Basic
{
    public class CPUBasic
    {
        public enum eMemRange
        {
            NONE,
            INPUT,
            OUTPUT,
            MARKER,
        }

        public enum eCPUCommands
        {
            NONE    = 0x00,     // 000
            L       = 0x01,     // 001
            T       = 0x02,     // 002
            LN      = 0x03,     // 003

            R       = 0x0A,     // 010
            S       = 0x0B,     // 011
            E       = 0x0C,     // 012
            EN      = 0x0D,     // 013   

            N       = 0x14,     // 020 not used for now

            A       = 0x1E,     // 030
            O       = 0x1F,     // 031
            AN      = 0x20,     // 032
            ON      = 0x21,     // 033
            XOR     = 0x22,     // 034

            JA      = 0x28,     // 040
            JC      = 0x29,     // 041
            JCN     = 0x2A,     // 042

            SET     = 0x3C,     // 060
            NOP     = 0x3D,     // 061
        }

        public enum eCPUErrors
        {
            NONE,                   // no error
            UNKOWN_CMD,             // cmd not konwn
            JUMP_AND_MEMORY,        // target is a jump and also has a memory location in IN/OUT/MARKER
            JUMP_AND_BITACCESS,     // target is a jump and also bitaccess
            BIT_OUT_OF_RANGE,       // target bit is not in range (0..15 bit)
            MEM_LEN_OUT_OF_RANGE,   // target memory length is not in range (1..8 byte)(1/2/4)
        }
		
		static public readonly Dictionary<string, Version> CMD_VERSION = new Dictionary<string, Version>();
		

		// to access the bit masks via the 4 bit value FLAG_BITFIELD_VAL
		static public readonly int[] BITVALUES = { 
                                                     0x0001,    // 00001
                                                     0x0002,    // 00002
                                                     0x0004,    // 00004
                                                     0x0008,    // 00008
                                                     0x0010,    // 00016
                                                     0x0020,    // 00032
                                                     0x0040,    // 00064
                                                     0x0080,    // 00128
                                                     0x0100,    // 00256
                                                     0x0200,    // 00512
                                                     0x0400,    // 01024
                                                     0x0800,    // 02048
                                                     0x1000,    // 04096
                                                     0x2000,    // 08192
                                                     0x4000,    // 16384
                                                     0x8000,    // 32768
                                                 };

        // Instruction size 1 byte command + 4 byte target
        static public readonly int INSTR_SIZE = 5;
        // Targetflags
        // (BIT 31 : 1=Memeorylocation; 2=Const Number [highest bit])
        static public readonly uint FLAG_MEM_OR_NUM = (uint)0x1 << 31;
        // (BIT 28 : 1=bit access; 0=non bit access BYTE/WORD/DWORD)
        static public readonly uint FLAG_BIT_ACCESS = 0x1 << 28;
        // (BIT 27 : 1=Jump address; 0=&& BIT 28=0 then BYTE/WORD/DWORD Address)
        static public readonly uint FLAG_JUMP_ADDRESS = 0x1 << 27;

        // (BIT 26 : 1=Marker address; 0=non marker address)
        static public readonly uint FLAG_MEM_MARKER = 0x1 << 26;
        // (BIT 25 : 1=Output address; 0=non output address)
        static public readonly uint FLAG_MEM_OUTPUT = 0x1 << 25;
        // (BIT 24 : 1=Input address; 0=non input address)
        static public readonly uint FLAG_MEM_INPUT = 0x1 << 24;

        // (BIT Access or Memorylocation length)
        // (BIT 28 = 1 : Bit access)
        // (GET BIT-FIELD [BIT23-20] Digit)
        static public readonly uint FLAG_BITFIELD_VAL = 0xF0 << 16;
        // (BIT 28 = 0 : Memorylocation length)
        // (BIT 23 : 1=not used; 0=not used)
        // (BIT 22 : 1=4xByte; 0=no DWORD)
        static public readonly uint FLAG_MEMLEN_DWORD = 0x1 << 22;
        // (BIT 21 : 1=2xByte; 0=no WORD)
        static public readonly uint FLAG_MEMLEN_WORD = 0x1 << 21;
        // (BIT 20 : 1=1xByte; 0=no BYTE)
        static public readonly uint FLAG_MEMLEN_BYTE = 0x1 << 20;

        // (BIT 19 - BIT 0 : Memadress)
        static public readonly int FLAG_MEMADR = 0x0FFFFF;

		// (BIT 30 - BIT 0 : const value)
		static public readonly int FLAG_CONSTVAL = 0x7FFFFFFF;

		public struct SCPUResult
        {
            public TimeSpan CycleTime;
            public int Instr;
        }
    }
}
