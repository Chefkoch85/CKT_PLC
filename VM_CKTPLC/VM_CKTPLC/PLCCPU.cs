using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

using CKT.VM.PLC_Basic;
using eCPUCommands = CKT.VM.PLC_Basic.CPUBasic.eCPUCommands;
using eMemRange = CKT.VM.PLC_Basic.CPUBasic.eMemRange;
using eCPUError = CKT.VM.PLC_Basic.CPUBasic.eCPUErrors; 

namespace CKT.VM.CKTPLC
{
    class PLCCPU
    {
        int m_iAkku1 = 0, m_iAkku2 = 0, m_iAkku3 = 0, m_iAkku4 = 0;
        int m_iInstrPointer = 0, m_iInstrBase = 0;
        int m_iStackPointer = 0, m_iStackBase = 0;

        int m_iVKE = 0;

        bool m_bLoop = false;
        int m_iLoopInstrAddress = 0;

        byte[] m_byaInstrMemory = null;
        byte[] m_byaStackMemory = null;
        byte[] m_byaInputMemory = null;
        byte[] m_byaOutputMemory = null;
        byte[] m_byaMarkerMemory = null;

        byte[] m_byaImageInputMemory = null;
        byte[] m_byaImageOutputMemory = null;

        bool m_bPause = false;
        bool m_bRuns = false;
        bool m_bRunError = false;

        eCPUError m_CurError = eCPUError.NONE;
        int m_CurErrorInstr = 0;

        TimeSpan m_CycleTime = TimeSpan.FromMilliseconds(0);

        static public readonly Version CPU_VERSION = new Version(0, 2);  // if build is "1" the version is in test

        BackgroundWorker CPUCycle = null;
        
        public void init(ref byte[] instrMem, ref byte[] stackMem, ref byte[] inMem, ref byte[] outMem, ref byte[] marMem)
        {
            m_iAkku1 = 0;
            m_iAkku2 = 0;
            m_iAkku3 = 0;
            m_iAkku4 = 0;
            m_iInstrPointer = 0;
            m_iInstrBase = 0;
            m_iStackPointer = 0;
            m_iStackBase = 0;

            m_byaInstrMemory = instrMem;
            m_byaStackMemory = stackMem;
            m_byaInputMemory = inMem;
            m_byaOutputMemory = outMem;
            m_byaMarkerMemory = marMem;

            m_byaImageInputMemory = new byte[m_byaInputMemory.Length];
            m_byaImageOutputMemory = new byte[m_byaOutputMemory.Length];

            CPUCycle = new BackgroundWorker();
            CPUCycle.DoWork += new DoWorkEventHandler(cycle);
            CPUCycle.ProgressChanged += new ProgressChangedEventHandler(CPUCycle_ProgressChanged);
            CPUCycle.RunWorkerCompleted += new RunWorkerCompletedEventHandler(cycleCompleted);
            CPUCycle.WorkerReportsProgress = true;
            CPUCycle.WorkerSupportsCancellation = true;
        }

        public TimeSpan CycleTime
        {
            get
            {
                return m_CycleTime;
            }
        }

        public bool RunError
        {
            get
            {
                return m_bRunError;
            }
            set
            {
                m_bRunError = value;
            }
        }

        public eCPUError CurrentError
        {
            get
            {
                return m_CurError;
            }
        }
        public int CurrentErrorInstr
        {
            get
            {
                return m_CurErrorInstr;
            }
        }

        void CPUCycle_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            PLC_Basic.CPUBasic.SCPUResult res = (PLC_Basic.CPUBasic.SCPUResult)e.UserState;
            if (e.ProgressPercentage > 0)
            {
                m_CurError = (eCPUError)e.ProgressPercentage;
                m_CurErrorInstr = res.Instr;
                m_bRunError = true;
            }
            m_CycleTime = res.CycleTime;
        }

        void cycleCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
                System.Diagnostics.Debug.WriteLine("CPU cycle stopped!");
            else
                System.Diagnostics.Debug.WriteLine("CPU cycle ended!");
        }

        public int run()
        {
            if (!m_bRuns && !m_bPause)
            {
                CPUCycle.RunWorkerAsync(m_bPause);
                m_bRuns = true;
                m_CurError = eCPUError.NONE;
                return 0;
            }

            return 1;
        }
        
        private void cycle(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = ((BackgroundWorker)sender);
            while (true)
            {
                eCPUError RunError = eCPUError.NONE;
                if (worker.CancellationPending)
                    return;

                DateTime start = DateTime.Now;

                copyInput();

                while (m_iInstrPointer < m_byaInstrMemory.Length)
                {
                    for (int i = 0; i < 100000; i++)
                    {
                        double d = Math.Sqrt(25 * 25);
                    }

                    if (worker.CancellationPending)
                        return;

                    byte cmd = m_byaInstrMemory[m_iInstrPointer];
                    uint iTarget = (uint)m_byaInstrMemory[m_iInstrPointer + 1] << 24;
                    iTarget |= (uint)m_byaInstrMemory[m_iInstrPointer + 2] << 16;
                    iTarget |= (uint)m_byaInstrMemory[m_iInstrPointer + 3] << 8;
                    iTarget |= (uint)m_byaInstrMemory[m_iInstrPointer + 4];

                    if (m_bLoop)
                    {
                        cmd = m_byaInstrMemory[m_iLoopInstrAddress];
                        iTarget = (uint)m_byaInstrMemory[m_iLoopInstrAddress + 1] << 24;
                        iTarget |= (uint)m_byaInstrMemory[m_iLoopInstrAddress + 2] << 16;
                        iTarget |= (uint)m_byaInstrMemory[m_iLoopInstrAddress + 3] << 8;
                        iTarget |= (uint)m_byaInstrMemory[m_iLoopInstrAddress + 4];
                    }

                    doInstr(cmd, iTarget, out RunError);

                    if (RunError != eCPUError.NONE)
                        break;
                }

                copyOutput();

                TimeSpan diff = DateTime.Now - start;
                PLC_Basic.CPUBasic.SCPUResult res = new CPUBasic.SCPUResult();
                res.CycleTime = diff;
                res.Instr = (RunError != eCPUError.NONE ? m_iInstrPointer / PLC_Basic.CPUBasic.INSTR_SIZE : 0);

                m_iInstrPointer = 0;

                if (sender is BackgroundWorker)
                    worker.ReportProgress((int)RunError, res);

                if (RunError != eCPUError.NONE)
                    worker.CancelAsync();

            }
        }

        private void copyInput()
        {
            Array.Copy(m_byaInputMemory, m_byaImageInputMemory, m_byaInputMemory.Length);
        }

        private void copyOutput()
        {
            Array.Copy(m_byaImageOutputMemory, m_byaOutputMemory, m_byaOutputMemory.Length);
        }

        private void doInstr(byte command, uint target, out CPUBasic.eCPUErrors err)
        {
            err = eCPUError.NONE;

            bool isMemLocation = (CPUBasic.FLAG_MEM_OR_NUM & target) > 0;
            bool isJump = (CPUBasic.FLAG_JUMP_ADDRESS & target) > 0;
            bool isBitAccess = (CPUBasic.FLAG_BIT_ACCESS & target) > 0;
            uint len = (CPUBasic.FLAG_BITFIELD_VAL & target) >> 20;
            eMemRange witchMem = (CPUBasic.FLAG_MEM_MARKER & target) > 0 ? eMemRange.MARKER : eMemRange.NONE;
            witchMem = (CPUBasic.FLAG_MEM_OUTPUT & target) > 0 ? eMemRange.OUTPUT : witchMem;
            witchMem = (CPUBasic.FLAG_MEM_INPUT & target) > 0 ? eMemRange.INPUT : witchMem;

            if (isJump && witchMem != eMemRange.NONE)
                err = eCPUError.JUMP_AND_MEMORY;

            if (isMemLocation && isBitAccess && len > 15 || isMemLocation && isBitAccess && len < 0)
                err = eCPUError.BIT_OUT_OF_RANGE;

            if (isMemLocation && !isJump && !isBitAccess && len > 4 || isMemLocation && !isJump && !isBitAccess && len < 1) 
                err = eCPUError.MEM_LEN_OUT_OF_RANGE;

            if (isBitAccess && isJump)
                err = eCPUError.JUMP_AND_BITACCESS;

            if (err != eCPUError.NONE)
                return;

            int value = 0;
            if (isMemLocation)
                value = (int)(CPUBasic.FLAG_MEMADR & target);
            else
                value = (int)(CPUBasic.FLAG_CONSTVAL & target);

            eCPUCommands cmd = (eCPUCommands)command;
            switch (cmd)
            {
                case eCPUCommands.L:
                    m_iAkku1 = getValue(isMemLocation, isBitAccess, witchMem, value, (int)len);
                    break;

                case eCPUCommands.T:
                    setValue(isBitAccess, witchMem, value, (int)len, m_iAkku1);
                    break;

                case eCPUCommands.LN:
                    if (isBitAccess)
                    {
                        m_iAkku1 = 1 - getValue(isMemLocation, isBitAccess, witchMem, value, (int)len);
                    }
                    break;

                case eCPUCommands.R:
                    if (isBitAccess && m_iAkku1 == 1)
                    {
                        setValue(isBitAccess, witchMem, value, (int)len, 0);
                    }
                    break;

                case eCPUCommands.S:
                    if (isBitAccess && m_iAkku1 == 1)
                    {
                        setValue(isBitAccess, witchMem, value, (int)len, 1);
                    }
                    break;

                case eCPUCommands.E:
                    if (isBitAccess)
                    {
                        setValue(isBitAccess, witchMem, value, (int)len, m_iAkku1);
                    }
                    break;

                case eCPUCommands.EN:
                    if (isBitAccess)
                    {
                        setValue(isBitAccess, witchMem, value, (int)len, 1 - m_iAkku1);
                    }
                    break;

                case eCPUCommands.A:
                    if (isBitAccess)
                    {
                        m_iAkku2 = m_iAkku1;
                        m_iAkku1 = getValue(isMemLocation, isBitAccess, witchMem, value, (int)len);
                        m_iAkku1 = m_iAkku1 & m_iAkku2;
                    }
                    break;

                case eCPUCommands.O:
                    if (isBitAccess)
                    {
                        m_iAkku2 = m_iAkku1;
                        m_iAkku1 = getValue(isMemLocation, isBitAccess, witchMem, value, (int)len);
                        m_iAkku1 = m_iAkku1 | m_iAkku2;
                    }
                    break;

                case eCPUCommands.AN:
                    if (isBitAccess)
                    {
                        m_iAkku2 = m_iAkku1;
                        m_iAkku1 = 1 - getValue(isMemLocation, isBitAccess, witchMem, value, (int)len);
                        m_iAkku1 = m_iAkku1 & m_iAkku2;
                    }
                    break;

                case eCPUCommands.ON:
                    if (isBitAccess)
                    {
                        m_iAkku2 = m_iAkku1;
                        m_iAkku1 = 1 - getValue(isMemLocation, isBitAccess, witchMem, value, (int)len);
                        m_iAkku1 = m_iAkku1 | m_iAkku2;
                    }
                    break;

                case eCPUCommands.XOR:
                    if (isBitAccess)
                    {
                        m_iAkku2 = m_iAkku1;
                        m_iAkku1 = 1 - getValue(isMemLocation, isBitAccess, witchMem, value, (int)len);
                        m_iAkku1 = m_iAkku1 ^ m_iAkku2;
                    }
                    break;

                case eCPUCommands.JA:
                    if (isJump)
                    {
                        if (len == 1)
                        {
                            m_iLoopInstrAddress = value * CPUBasic.INSTR_SIZE + (m_iInstrBase);
                            m_iInstrPointer = m_byaInstrMemory.Length - 1;
                            m_bLoop = true;
                            break;
                        }
                        m_iInstrPointer = value * CPUBasic.INSTR_SIZE + (m_iInstrBase - CPUBasic.INSTR_SIZE);
                    }
                    break;

                case eCPUCommands.JC:
                    if (isJump && m_iAkku1 == 1)
                    {
                        if (len == 1)
                        {
                            m_iLoopInstrAddress = value * CPUBasic.INSTR_SIZE + (m_iInstrBase);
                            m_iInstrPointer = m_byaInstrMemory.Length - 1;
                            m_bLoop = true;
                            break;
                        }
                        m_iInstrPointer = value * CPUBasic.INSTR_SIZE + (m_iInstrBase - CPUBasic.INSTR_SIZE);
                    }
                    break;

                case eCPUCommands.JCN:
                    if (isJump && m_iAkku1 == 0)
                    {
                        if (len == 1)
                        {
                            m_iLoopInstrAddress = value * CPUBasic.INSTR_SIZE + (m_iInstrBase);
                            m_iInstrPointer = m_byaInstrMemory.Length - 1;
                            m_bLoop = true;
                            break;
                        }
                        m_iInstrPointer = value * CPUBasic.INSTR_SIZE + (m_iInstrBase - CPUBasic.INSTR_SIZE);
                    }
                    break;

                case eCPUCommands.SET:
                    m_iAkku1 = 1;
                    break;

                case eCPUCommands.NOP:
                    // nothing to do empty command
                    break;

                default:
                    err = eCPUError.UNKOWN_CMD;
                    break;
            }

            m_iInstrPointer += CPUBasic.INSTR_SIZE;
        }

        private int getValue(bool isMem, bool isBit, eMemRange range, int targetVal, int bitfield)
        {
            int akkuVal = 0;
            if (isMem)
                akkuVal = getValueMem(isBit, range, targetVal, bitfield);
            else
                akkuVal = targetVal;

            return akkuVal;
        }

        private int getValueMem(bool isBit, eMemRange range, int targetVal, int bitfield)
        {
            int akkuVal = 0;
            switch (range)
            {
                case eMemRange.MARKER:
                    if (isBit)
                    {
                        akkuVal = (m_byaMarkerMemory[targetVal] & CPUBasic.BITVALUES[bitfield]) >> bitfield;
                    }
                    else
                    {
                        for (int i = 0; i < bitfield; i++)
                        {
                            akkuVal |= m_byaMarkerMemory[targetVal + i] << (i * 8);
                        }
                    }
                    break;

                case eMemRange.OUTPUT:
                    if (isBit)
                    {
                        akkuVal = (m_byaImageOutputMemory[targetVal] & CPUBasic.BITVALUES[bitfield]) >> bitfield;
                    }
                    else
                    {
                        for (int i = 0; i < bitfield; i++)
                        {
                            akkuVal |= m_byaImageOutputMemory[targetVal + i] << (i * 8);
                        }
                    }
                    break;

                case eMemRange.INPUT:
                    if (isBit)
                    {
                        akkuVal = (m_byaImageInputMemory[targetVal] & CPUBasic.BITVALUES[bitfield]) >> bitfield;
                    }
                    else
                    {
                        for (int i = 0; i < bitfield; i++)
                        {
                            akkuVal |= m_byaImageInputMemory[targetVal + i] << (i * 8);
                        }
                    }
                    break;
            }

            return akkuVal;
        }

        private void setValue(bool isBit, eMemRange range, int targetVal, int bitfield, int akku1)
        {
            byte akkuAsByte = 0;
            byte[] akkuAsBits = new byte[16];
            switch (range)
            {
                case eMemRange.MARKER:
                    if (isBit)
                    {
                        byte bitByte = m_byaMarkerMemory[targetVal];
                        for(int i = 0; i < 16; i++)
                            //TODO: check if it has to be so or like it is now
                            // akkuAsBits[i] = (byte)(bitByte & BITVALUES[i]);
                            akkuAsBits[i] = (byte)((bitByte & CPUBasic.BITVALUES[i]) >> (i));

                        bitByte = 0;
                        akkuAsBits[bitfield] = (byte)akku1;

                        for (int i = 0; i < 16; i++)
                            bitByte |= (byte)(akkuAsBits[i] << (i));

                        m_byaMarkerMemory[targetVal] = bitByte;
                    }
                    else
                    {
                        for (int i = 0; i < bitfield; i++)
                        {
                            akkuAsByte = (byte)((akku1 & (0xFF << (i * 8))) >> (i * 8));
                            m_byaMarkerMemory[targetVal + i] = akkuAsByte;
                        }
                    }
                    break;

                case eMemRange.OUTPUT:
                    if (isBit)
                    {
                        byte bitByte = m_byaImageOutputMemory[targetVal];
                        for (int i = 0; i < 16; i++)
                            akkuAsBits[i] = (byte)((bitByte & CPUBasic.BITVALUES[i]) >> (i));

                        bitByte = 0;
                        akkuAsBits[bitfield] = (byte)akku1;

                        for (int i = 0; i < 16; i++)
                            bitByte |= (byte)(akkuAsBits[i] << (i));

                        m_byaImageOutputMemory[targetVal] = bitByte;
                    }
                    else
                    {
                        for (int i = 0; i < bitfield; i++)
                        {
                            akkuAsByte = (byte)((akku1 & (0xFF << (i * 8))) >> (i * 8));
                            m_byaImageOutputMemory[targetVal + i] = akkuAsByte;
                        }
                    }
                    break;

                case eMemRange.INPUT:
                    if (isBit)
                    {
                        byte bitByte = m_byaImageInputMemory[targetVal];
                        for (int i = 0; i < 16; i++)
                            akkuAsBits[i] = (byte)((bitByte & CPUBasic.BITVALUES[i]) >> (i));

                        bitByte = 0;
                        akkuAsBits[bitfield] = (byte)akku1;

                        for (int i = 0; i < 16; i++)
                            bitByte |= (byte)(akkuAsBits[i] << (i));

                        m_byaImageInputMemory[targetVal] = bitByte;
                    }
                    else
                    {
                        for (int i = 0; i < bitfield; i++)
                        {
                            akkuAsByte = (byte)((akku1 & (0xFF << (i * 8))) >> (i * 8));
                            m_byaImageInputMemory[targetVal + i] = akkuAsByte;
                        }
                    }
                    break;
            }
        }

        public void reset()
        {
            m_iAkku1 = 0;
            m_iAkku2 = 0;
            m_iAkku3 = 0;
            m_iAkku4 = 0;
            m_iInstrPointer = 0;
            m_iStackPointer = 0;

            m_bRuns = false;
            m_bPause = false;
        }

        public void pause()
        {
            m_bPause = !m_bPause;
            
            if (m_bPause)
            {
                CPUCycle.CancelAsync();
                m_bRuns = false;
            }
        }

    }
}
