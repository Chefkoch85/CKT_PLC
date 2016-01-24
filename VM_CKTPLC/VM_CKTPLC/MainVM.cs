using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CKT.VM.CKTPLC
{
    class MainVM
    {
        static void Main(string[] args)
        {
            PLCManager.instance.runPLCMenu();
        }
    }
}
