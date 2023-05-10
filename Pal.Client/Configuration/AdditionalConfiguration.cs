using ECommons;
using ECommons.Configuration;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Pal.Client.Configuration
{
    public class AdditionalConfiguration : IEzConfig
    {
        public bool DisplayExit = false;
        public bool DisplayExitOnlyActive = false;
        public bool TrapColorFilled = false;
        public bool BronzeShow = false;
        public bool BronzeFill = false;
        public Vector4 BronzeColor = 4279786209.ToVector4();
        public Vector4 TrapColor = 0xFF0000FF.ToVector4();
    }
}
