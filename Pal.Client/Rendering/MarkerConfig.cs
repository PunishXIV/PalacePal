using System.Collections.Generic;
using Pal.Client.Floors;

namespace Pal.Client.Rendering
{
    internal sealed class MarkerConfig
    {
        private static readonly MarkerConfig EmptyConfig = new();

        private static readonly Dictionary<MemoryLocation.EType, MarkerConfig> MarkerConfigs = new()
        {
            { MemoryLocation.EType.Trap, new MarkerConfig { Radius = 1.7f } },
            { MemoryLocation.EType.Hoard, new MarkerConfig { Radius = 1.7f, OffsetY = -0.03f } },
            { MemoryLocation.EType.SilverCoffer, new MarkerConfig { Radius = 1f } },
            { MemoryLocation.EType.GoldCoffer, new MarkerConfig { Radius = 1f } },
        };

        public float OffsetY { get; private init; }
        public float Radius { get; private init; } = 0.25f;

        public static MarkerConfig ForType(MemoryLocation.EType type) =>
            MarkerConfigs.GetValueOrDefault(type, EmptyConfig);
    }
}
