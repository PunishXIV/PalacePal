using System;
using System.Collections.Generic;
using System.Linq;

namespace Pal.Client.Configuration
{
    public sealed class ConfigurationV7 : IPalacePalConfiguration, IConfigurationInConfigDirectory
    {
        public int Version { get; set; } = 7;

        public bool FirstUse { get; set; } = true;
        public EMode Mode { get; set; }
        public string BetaKey { get; init; } = "";

        public DeepDungeonConfiguration DeepDungeons { get; set; } = new();
        public RendererConfiguration Renderer { get; set; } = new();
        public List<AccountConfigurationV7> Accounts { get; set; } = new();
        public BackupConfiguration Backups { get; set; } = new();

        public IAccountConfiguration CreateAccount(string server, Guid accountId)
        {
            var account = new AccountConfigurationV7(server, accountId);
            Accounts.Add(account);
            return account;
        }

        [Obsolete("for V1 import")]
        internal IAccountConfiguration CreateAccount(string server, string accountId)
        {
            var account = new AccountConfigurationV7(server, accountId);
            Accounts.Add(account);
            return account;
        }

        public IAccountConfiguration? FindAccount(string server)
        {
            return Accounts.FirstOrDefault(a => a.Server == server && a.IsUsable);
        }

        public void RemoveAccount(string server)
        {
            Accounts.RemoveAll(a => a.Server == server && a.IsUsable);
        }

        public bool HasRoleOnCurrentServer(string server, string role)
        {
            if (Mode != EMode.Online)
                return false;

            var account = FindAccount(server);
            return account == null || account.CachedRoles.Contains(role);
        }
    }
}
