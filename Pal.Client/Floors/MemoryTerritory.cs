using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Pal.Client.Configuration;
using Pal.Client.Scheduled;
using Pal.Common;

namespace Pal.Client.Floors
{
    /// <summary>
    /// A single set of floors loaded entirely in memory, can be e.g. POTD 51-60.
    /// </summary>
    internal sealed class MemoryTerritory
    {
        public MemoryTerritory(ETerritoryType territoryType)
        {
            TerritoryType = territoryType;
        }

        public ETerritoryType TerritoryType { get; }
        public EReadyState ReadyState { get; set; } = EReadyState.NotLoaded;
        public ESyncState SyncState { get; set; } = ESyncState.NotAttempted;

        public ConcurrentBag<PersistentLocation> Locations { get; } = new();
        public object LockObj { get; } = new();

        public void Initialize(IEnumerable<PersistentLocation> locations)
        {
            Locations.Clear();
            foreach (var location in locations)
                Locations.Add(location);

            ReadyState = EReadyState.Ready;
        }

        public void Reset()
        {
            Locations.Clear();
            SyncState = ESyncState.NotAttempted;
            ReadyState = EReadyState.NotLoaded;
        }

        public enum EReadyState
        {
            NotLoaded,

            /// <summary>
            /// Currently loading from the database.
            /// </summary>
            Loading,

            /// <summary>
            /// Locations loaded, no import running.
            /// </summary>
            Ready,

            /// <summary>
            /// Import running, should probably not interact with this too much.
            /// </summary>
            Importing,
        }
    }
}
