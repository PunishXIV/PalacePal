using ECommons;
using ECommons.Configuration;
using Dalamud.Bindings.ImGui;
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
        public bool GoldText = true;
        public bool SilverText = true;
        public bool DisplayExit = false;
        public bool DisplayExitOnlyActive = false;
        public bool ExitText = true;
        public bool TrapColorFilled = false;
        public bool BronzeShow = false;
        public bool BronzeFill = false;
        public bool BronzeText = true;
        public Vector4 BronzeColor = 0xFF185AE1.ToVector4();
        public Vector4 MimicColor = 0xFF0000FF.ToVector4();
        public Vector4 TrapColor = 0xFF0000FF.ToVector4();
        public Vector4 ExitColor = 0xFFFF00C8.ToVector4();
        public float OverlayFScale = 1.3f;
    }
}
