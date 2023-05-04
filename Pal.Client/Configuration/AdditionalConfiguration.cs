using ECommons.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pal.Client.Configuration
{
    public class AdditionalConfiguration : IEzConfig
    {
        public bool DisplayExit = false;
        public bool DisplayExitOnlyActive = false;
    }
}
