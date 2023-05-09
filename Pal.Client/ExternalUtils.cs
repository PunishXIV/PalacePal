using ECommons.DalamudServices;
using ECommons.SplatoonAPI;
using Pal.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pal.Client
{
    internal static unsafe class ExternalUtils
    {
        static readonly uint[] BronzeCofferDataID = new uint[] { 782, 783, 784, 785, 786, 787, 788, 789, 790, 802, 803, 804, 805, 1036, 1037, 1038, 1039, 1040, 1041, 1042, 1043, 1044, 1045, 1046, 1047, 1048, 1049, 1541, 1542, 1543, 1544, 1545, 1546, 1547, 1548, 1549, 1550, 1551, 1552, 1553, 1554 };
        const string BronzeTreasureNamespace = "PalacePal.BronzeTreasure";


        internal static void UpdateBronzeTreasureCoffers(uint terr)
        {
            Splatoon.RemoveDynamicElements(BronzeTreasureNamespace);
            if (Enum.GetValues<ETerritoryType>().Contains((ETerritoryType)terr))
            {
                foreach(var x in BronzeCofferDataID)
                {
                    var element = Splatoon.DecodeElement("{\"Name\":\"Bronze Treasure Coffer\",\"type\":1,\"color\":4279786209,\"overlayBGColor\":0,\"overlayTextColor\":4279786209,\"overlayVOffset\":0.6,\"overlayFScale\":1.3,\"overlayText\":\" Bronze Treasure Coffer\",\"refActorComparisonType\":3,\"includeOwnHitbox\":true}");
                    element.refActorDataID = x;
                    var elementFill = Splatoon.DecodeElement("{\"Name\":\"Bronze Treasure Coffer Fill\",\"type\":1,\"color\":840456929,\"overlayVOffset\":0.68,\"overlayFScale\":1.24,\"FillStep\":0.429,\"refActorComparisonType\":3,\"includeOwnHitbox\":true,\"Filled\":true}");
                    elementFill.refActorDataID = x;
                    Splatoon.AddDynamicElement(BronzeTreasureNamespace, element, 0);
                    Splatoon.AddDynamicElement(BronzeTreasureNamespace, elementFill, 0);
                }
                Splatoon.AddDynamicElement(BronzeTreasureNamespace, Splatoon.DecodeElement("{\"Name\":\"Mimic Trap Coffer\",\"type\":1,\"color\":4278190335,\"overlayBGColor\":0,\"overlayTextColor\":4278190335,\"overlayVOffset\":0.6,\"overlayFScale\":1.3,\"overlayText\":\" Mimic Trap Coffer\",\"refActorDataID\":2006020,\"FillStep\":0.029,\"refActorComparisonType\":3,\"includeOwnHitbox\":true,\"AdditionalRotation\":0.43633232}"), 0);
                Splatoon.AddDynamicElement(BronzeTreasureNamespace, Splatoon.DecodeElement("{\"Name\":\"Mimic Trap Coffer Fill\",\"type\":1,\"color\":838861055,\"overlayBGColor\":0,\"overlayTextColor\":4278190335,\"overlayVOffset\":0.6,\"overlayFScale\":1.3,\"refActorDataID\":2006020,\"FillStep\":0.029,\"refActorComparisonType\":3,\"includeOwnHitbox\":true,\"AdditionalRotation\":0.43633232,\"Filled\":true}"), 0);
            }
        }
    }
}
