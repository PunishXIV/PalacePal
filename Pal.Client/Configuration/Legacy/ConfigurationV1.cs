using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Pal.Client.Configuration.Legacy
{
    [Obsolete]
    public sealed class ConfigurationV1
    {
        public int Version { get; set; } = 6;

        #region Saved configuration values
        public bool FirstUse { get; set; } = true;
        public EMode Mode { get; set; } = EMode.Offline;
        public ERenderer Renderer { get; set; } = ERenderer.Splatoon;

        [Obsolete]
        public string? DebugAccountId { private get; set; }

        [Obsolete]
        public string? AccountId { private get; set; }

        [Obsolete]
        public Dictionary<string, Guid> AccountIds { private get; set; } = new();
        public Dictionary<string, AccountInfo> Accounts { get; set; } = new();

        public List<ImportHistoryEntry> ImportHistory { get; set; } = new();

        public bool ShowTraps { get; set; } = true;
        public Vector4 TrapColor { get; set; } = new(1, 0, 0, 0.4f);
        public bool OnlyVisibleTrapsAfterPomander { get; set; } = true;

        public bool ShowHoard { get; set; } = true;
        public Vector4 HoardColor { get; set; } = new(0, 1, 1, 0.4f);
        public bool OnlyVisibleHoardAfterPomander { get; set; } = true;

        public bool ShowSilverCoffers { get; set; }
        public Vector4 SilverCofferColor { get; set; } = new(1, 1, 1, 0.4f);
        public bool FillSilverCoffers { get; set; } = true;

        /// <summary>
        /// Needs to be manually set.
        /// </summary>
        public string BetaKey { get; set; } = "";
        #endregion

        public void Migrate(DalamudPluginInterface pluginInterface, ILogger<ConfigurationV1> logger)
        {
            if (Version == 1)
            {
                logger.LogInformation("Updating config to version 2");

                if (DebugAccountId != null && Guid.TryParse(DebugAccountId, out Guid debugAccountId))
                    AccountIds["http://localhost:5145"] = debugAccountId;

                if (AccountId != null && Guid.TryParse(AccountId, out Guid accountId))
                    AccountIds["https://pal.μ.tv"] = accountId;

                Version = 2;
                Save(pluginInterface);
            }

            if (Version == 2)
            {
                logger.LogInformation("Updating config to version 3");

                Accounts = AccountIds.ToDictionary(x => x.Key, x => new AccountInfo
                {
                    Id = x.Value.ToString() // encryption happens in V7 migration at latest
                });
                Version = 3;
                Save(pluginInterface);
            }

            if (Version == 3)
            {
                Version = 4;
                Save(pluginInterface);
            }

            if (Version == 4)
            {
                // 2.2 had a bug that would mark chests as traps, there's no easy way to detect this -- or clean this up.
                // Not a problem for online players, but offline players might be fucked.
                //bool changedAnyFile = false;
                JsonFloorState.ForEach(s =>
                {
                    foreach (var marker in s.Markers)
                        marker.SinceVersion = "0.0";

                    var lastModified = File.GetLastWriteTimeUtc(s.GetSaveLocation());
                    if (lastModified >= new DateTime(2023, 2, 3, 0, 0, 0, DateTimeKind.Utc))
                    {
                        s.Backup(suffix: "bak");

                        s.Markers = new ConcurrentBag<JsonMarker>(s.Markers.Where(m => m.SinceVersion != "0.0" || m.Type == JsonMarker.EType.Hoard || m.WasImported));
                        s.Save();

                        //changedAnyFile = true;
                    }
                    else
                    {
                        // just add version information, nothing else
                        s.Save();
                    }
                });

                /*
                // Only notify offline users - we can just re-download the backup markers from the server seamlessly.
                if (Mode == EMode.Offline && changedAnyFile)
                {
                    _ = new TickScheduler(delegate
                    {
                        Service.Chat.PalError("Due to a bug, some coffers were accidentally saved as traps. To fix the related display issue, locally cached data was cleaned up.");
                        Service.Chat.PrintError($"If you have any backup tools installed, please restore the contents of '{Service.PluginInterface.GetPluginConfigDirectory()}' to any backup from February 2, 2023 or before.");
                        Service.Chat.PrintError("You can also manually restore .json.bak files (by removing the '.bak') if you have not been in any deep dungeon since February 2, 2023.");
                    }, 2500);
                }
                */

                Version = 5;
                Save(pluginInterface);
            }

            if (Version == 5)
            {
                JsonFloorState.UpdateAll();

                Version = 6;
                Save(pluginInterface);
            }
        }

        public void Save(DalamudPluginInterface pluginInterface)
        {
            File.WriteAllText(pluginInterface.ConfigFile.FullName, JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                TypeNameHandling = TypeNameHandling.Objects
            }));
        }

        public sealed class AccountInfo
        {
            public string? Id { get; set; }
            public List<string> CachedRoles { get; set; } = new();
        }

        public sealed class ImportHistoryEntry
        {
            public Guid Id { get; set; }
            public string? RemoteUrl { get; set; }
            public DateTime ExportedAt { get; set; }

            /// <summary>
            /// Set when the file is imported locally.
            /// </summary>
            public DateTime ImportedAt { get; set; }
        }
    }
}
