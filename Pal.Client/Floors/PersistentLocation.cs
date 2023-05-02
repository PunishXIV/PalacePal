using System;
using System.Collections.Generic;
using Pal.Client.Database;

namespace Pal.Client.Floors
{
    /// <summary>
    /// A <see cref="ClientLocation"/> loaded in memory, with certain extra attributes as needed.
    /// </summary>
    internal sealed class PersistentLocation : MemoryLocation
    {
        /// <see cref="ClientLocation.LocalId"/>
        public int? LocalId { get; set; }

        /// <summary>
        /// Network id for the server you're currently connected to.
        /// </summary>
        public Guid? NetworkId { get; set; }

        /// <summary>
        /// For markers that the server you're connected to doesn't know: Whether this was requested to be uploaded, to avoid duplicate requests.
        /// </summary>
        public bool UploadRequested { get; set; }

        /// <see cref="ClientLocation.RemoteEncounters"/>
        ///
        public List<string> RemoteSeenOn { get; set; } = new();

        /// <summary>
        /// Whether this marker was requested to be seen, to avoid duplicate requests.
        /// </summary>
        public bool RemoteSeenRequested { get; set; }

        public ClientLocation.ESource Source { get; init; }

        public override bool Equals(object? obj) => obj is PersistentLocation && base.Equals(obj);

        public override int GetHashCode() => base.GetHashCode();

        public static bool operator ==(PersistentLocation? a, object? b)
        {
            return Equals(a, b);
        }

        public static bool operator !=(PersistentLocation? a, object? b)
        {
            return !Equals(a, b);
        }

        public override string ToString()
        {
            return $"PersistentLocation(Position={Position}, Type={Type})";
        }
    }
}
