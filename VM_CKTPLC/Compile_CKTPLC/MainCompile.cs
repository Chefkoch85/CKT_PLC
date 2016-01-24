using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CKT.VM.COMPILE
{
    class MainCompile
    {
        static void Main(string[] args)
        {
			App.instance.CmdLineArgs(args);
            App.instance.run();
        }
    }
}
