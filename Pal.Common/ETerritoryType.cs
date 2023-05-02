using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Pal.Common
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum ETerritoryType : ushort
    {
        Palace_1_10 = 561,
        Palace_11_20,
        Palace_21_30,
        Palace_31_40,
        Palace_41_50,
        Palace_51_60 = 593,
        Palace_61_70,
        Palace_71_80,
        Palace_81_90,
        Palace_91_100,
        Palace_101_110,
        Palace_111_120,
        Palace_121_130,
        Palace_131_140,
        Palace_141_150,
        Palace_151_160,
        Palace_161_170,
        Palace_171_180,
        Palace_181_190,
        Palace_191_200,

        [Display(Order = 1)]
        HeavenOnHigh_1_10 = 770,
        [Display(Order = 2)]
        HeavenOnHigh_11_20 = 771,
        [Display(Order = 3)]
        HeavenOnHigh_21_30 = 772,
        [Display(Order = 4)]
        HeavenOnHigh_31_40 = 782,
        [Display(Order = 5)]
        HeavenOnHigh_41_50 = 773,
        [Display(Order = 6)]
        HeavenOnHigh_51_60 = 783,
        [Display(Order = 7)]
        HeavenOnHigh_61_70 = 774,
        [Display(Order = 8)]
        HeavenOnHigh_71_80 = 784,
        [Display(Order = 9)]
        HeavenOnHigh_81_90 = 775,
        [Display(Order = 10)]
        HeavenOnHigh_91_100 = 785
    }
}
