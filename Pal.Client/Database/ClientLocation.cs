using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Pal.Client.Database
{
    internal sealed class ClientLocation
    {
        [Key] public int LocalId { get; set; }
        public ushort TerritoryType { get; set; }
        public EType Type { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        /// <summary>
        /// Whether we have encountered the trap/coffer at this location in-game.
        /// </summary>
        public bool Seen { get; set; }

        /// <summary>
        /// Which account ids this marker was seen. This is a list merely to support different remote endpoints
        /// (where each server would assign you a different id).
        /// </summary>
        public List<RemoteEncounter> RemoteEncounters { get; set; } = new();

        /// <summary>
        /// To keep track of which markers were imported through a downloaded file, we save the associated import-id.
        ///
        /// Importing another file for the same remote server will remove the old import-id, and add the new import-id here.
        /// </summary>
        public List<ImportHistory> ImportedBy { get; set; } = new();

        /// <summary>
        /// Determines where this location is originally from.
        /// </summary>
        public ESource Source { get; set; }


        /// <summary>
        /// To make rollbacks of local data easier, keep track of the plugin version which was used to create this location initially.
        /// </summary>
        public string SinceVersion { get; set; } = "0.0";

        public enum EType
        {
            Trap = 1,
            Hoard = 2,
        }

        public enum ESource
        {
            Unknown = 0,
            SeenLocally = 1,
            ExplodedLocally = 2,
            Import = 3,
            Download = 4,
        }
    }
}
