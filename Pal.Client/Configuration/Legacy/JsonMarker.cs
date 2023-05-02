using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pal.Client.Configuration.Legacy
{
    [Obsolete]
    public class JsonMarker
    {
        public EType Type { get; set; } = EType.Unknown;
        public Vector3 Position { get; set; }
        public bool Seen { get; set; }
        public List<string> RemoteSeenOn { get; set; } = new();
        public List<Guid> Imports { get; set; } = new();
        public bool WasImported { get; set; }
        public string? SinceVersion { get; set; }

        public enum EType
        {
            Unknown = 0,
            Trap = 1,
            Hoard = 2,
            Debug = 3,
        }
    }
}
