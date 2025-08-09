using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;

namespace Pal.Client.Configuration
{
    public interface IVersioned
    {
        int Version { get; set; }
    }
    public interface IConfigurationInConfigDirectory : IVersioned
    {
    }

    public interface IPalacePalConfiguration : IConfigurationInConfigDirectory
    {
        bool FirstUse { get; set; }
        EMode Mode { get; set; }
        string BetaKey { get; }

        DeepDungeonConfiguration DeepDungeons { get; set; }
        BackupConfiguration Backups { get; set; }

        IAccountConfiguration CreateAccount(string server, Guid accountId);
        IAccountConfiguration? FindAccount(string server);
        void RemoveAccount(string server);

        bool HasRoleOnCurrentServer(string server, string role);
    }

    public class DeepDungeonConfiguration
    {
        public MarkerConfiguration Traps { get; set; } = new()
        {
            Show = true,
            Color = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.4f)),
            OnlyVisibleAfterPomander = true,
            Fill = false
        };

        public MarkerConfiguration HoardCoffers { get; set; } = new()
        {
            Show = true,
            Color = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, 0.4f)),
            OnlyVisibleAfterPomander = true,
            Fill = false
        };

        public MarkerConfiguration SilverCoffers { get; set; } = new()
        {
            Show = false,
            Color = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.4f)),
            OnlyVisibleAfterPomander = false,
            Fill = true
        };

        public MarkerConfiguration GoldCoffers { get; set; } = new()
        {
            Show = false,
            Color = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 0.4f)),
            OnlyVisibleAfterPomander = false,
            Fill = true
        };
    }

    public class MarkerConfiguration
    {
        [JsonRequired]
        public bool Show { get; set; }

        [JsonRequired]
        public uint Color { get; set; }

        public bool OnlyVisibleAfterPomander { get; set; }
        public bool Fill { get; set; }
    }

    public interface IAccountConfiguration
    {
        bool IsUsable { get; }
        string Server { get; }
        Guid AccountId { get; }

        /// <summary>
        /// This is taken from the JWT, and is only refreshed on a successful login.
        ///
        /// If you simply reload the plugin without any server interaction, this doesn't change.
        ///
        /// This has no impact on what roles the JWT actually contains, but is just to make it
        /// easier to draw a consistent UI. The server will still reject unauthorized calls.
        /// </summary>
        List<string> CachedRoles { get; set; }

        bool EncryptIfNeeded();
    }

    public class BackupConfiguration
    {
        public int MinimumBackupsToKeep { get; set; } = 3;
        public int DaysToDeleteAfter { get; set; } = 21;
    }
}
