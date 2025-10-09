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
    public class AdditionalConfiguration
    {
        public bool GoldShow = false;
        public bool GoldFill = false;
        public bool GoldText = true;
        public bool SilverShow = false;
        public bool SilverFill = false;
        public bool SilverText = true;
        public bool DisplayExit = false;
        public bool DisplayExitOnlyActive = false;
        public bool ExitText = true;
        public bool TrapColorFilled = false;
        public bool BronzeShow = false;
        public bool BronzeFill = false;
        public bool BronzeText = true;
        public Vector4 GoldColor = 0xFFD4AF37.ToVector4();
        public Vector4 SilverColor = 0xFFC0C0C0.ToVector4();
        public Vector4 BronzeColor = 0xFFCD7F32.ToVector4();
        public Vector4 MimicColor = 0xFF0000FF.ToVector4();
        public Vector4 TrapColor = 0xFF0000FF.ToVector4();
        public Vector4 ExitColor = 0xFFFF00C8.ToVector4();
        public float OverlayFScale = 1.3f;
    }
}
