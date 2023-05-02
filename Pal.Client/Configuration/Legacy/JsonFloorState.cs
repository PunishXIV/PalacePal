using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Pal.Client.Extensions;
using Pal.Common;

namespace Pal.Client.Configuration.Legacy
{
    /// <summary>
    /// Legacy JSON file for marker locations.
    /// </summary>
    [Obsolete]
    public sealed class JsonFloorState
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new() { IncludeFields = true };
        private const int CurrentVersion = 4;

        private static string _pluginConfigDirectory = null!;
        private static readonly EMode _mode = EMode.Online; // might not be true, but this is 'less strict filtering' for migrations

        internal static void SetContextProperties(string pluginConfigDirectory)
        {
            _pluginConfigDirectory = pluginConfigDirectory;
        }

        public ushort TerritoryType { get; set; }
        public ConcurrentBag<JsonMarker> Markers { get; set; } = new();

        public JsonFloorState(ushort territoryType)
        {
            TerritoryType = territoryType;
        }

        private void ApplyFilters()
        {
            if (_mode == EMode.Offline)
                Markers = new ConcurrentBag<JsonMarker>(Markers.Where(x => x.Seen || (x.WasImported && x.Imports.Count > 0)));
            else
                // ensure old import markers are removed if they are no longer part of a "current" import
                // this MAY remove markers the server sent you (and that you haven't seen), but this should be fixed the next time you enter the zone
                Markers = new ConcurrentBag<JsonMarker>(Markers.Where(x => x.Seen || !x.WasImported || x.Imports.Count > 0));
        }

        public static JsonFloorState? Load(ushort territoryType)
        {
            string path = GetSaveLocation(territoryType);
            if (!File.Exists(path))
                return null;

            string content = File.ReadAllText(path);
            if (content.Length == 0)
                return null;

            JsonFloorState localState;
            int version = 1;
            if (content[0] == '[')
            {
                // v1 only had a list of markers, not a JSON object as root
                localState = new JsonFloorState(territoryType)
                {
                    Markers = new ConcurrentBag<JsonMarker>(JsonSerializer.Deserialize<HashSet<JsonMarker>>(content, JsonSerializerOptions) ?? new()),
                };
            }
            else
            {
                var save = JsonSerializer.Deserialize<SaveFile>(content, JsonSerializerOptions);
                if (save == null)
                    return null;

                localState = new JsonFloorState(territoryType)
                {
                    Markers = new ConcurrentBag<JsonMarker>(save.Markers.Where(o => o.Type == JsonMarker.EType.Trap || o.Type == JsonMarker.EType.Hoard)),
                };
                version = save.Version;
            }

            localState.ApplyFilters();

            if (version <= 3)
            {
                foreach (var marker in localState.Markers)
                    marker.RemoteSeenOn = marker.RemoteSeenOn.Select(x => x.ToPartialId()).ToList();
            }

            if (version < CurrentVersion)
                localState.Save();

            return localState;
        }

        public void Save()
        {
            string path = GetSaveLocation(TerritoryType);

            ApplyFilters();
            SaveImpl(path);
        }

        public void Backup(string suffix)
        {
            string path = $"{GetSaveLocation(TerritoryType)}.{suffix}";
            if (!File.Exists(path))
            {
                SaveImpl(path);
            }
        }

        private void SaveImpl(string path)
        {
            foreach (var marker in Markers)
            {
                if (string.IsNullOrEmpty(marker.SinceVersion))
                    marker.SinceVersion = typeof(Plugin).Assembly.GetName().Version!.ToString(2);
            }

            if (Markers.Count == 0)
                File.Delete(path);
            else
            {
                File.WriteAllText(path, JsonSerializer.Serialize(new SaveFile
                {
                    Version = CurrentVersion,
                    Markers = new HashSet<JsonMarker>(Markers)
                }, JsonSerializerOptions));
            }
        }

        public string GetSaveLocation() => GetSaveLocation(TerritoryType);

        private static string GetSaveLocation(uint territoryType) => Path.Join(_pluginConfigDirectory, $"{territoryType}.json");

        public static void ForEach(Action<JsonFloorState> action)
        {
            foreach (ETerritoryType territory in typeof(ETerritoryType).GetEnumValues())
            {
                // we never had markers for eureka orthos, so don't bother
                if (territory > ETerritoryType.HeavenOnHigh_91_100)
                    break;

                JsonFloorState? localState = Load((ushort)territory);
                if (localState != null)
                    action(localState);
            }
        }

        public static void UpdateAll()
        {
            ForEach(s => s.Save());
        }

        public void UndoImport(List<Guid> importIds)
        {
            // When saving a floor state, any markers not seen, not remote seen, and not having an import id are removed;
            // so it is possible to remove "wrong" markers by not having them be in the current import.
            foreach (var marker in Markers)
                marker.Imports.RemoveAll(importIds.Contains);
        }

        public sealed class SaveFile
        {
            public int Version { get; set; }
            public HashSet<JsonMarker> Markers { get; set; } = new();
        }
    }
}
