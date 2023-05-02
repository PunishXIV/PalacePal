using System;
using System.Collections.Generic;
using ECommons.Schedulers;
using Pal.Client.Windows;

namespace Pal.Client.Commands
{
    internal sealed class PalTestConnectionCommand : ISubCommand
    {
        private readonly ConfigWindow _configWindow;

        public PalTestConnectionCommand(ConfigWindow configWindow)
        {
            _configWindow = configWindow;
        }

        public IReadOnlyDictionary<string, Action<string>> GetHandlers()
            => new Dictionary<string, Action<string>>
            {
                { "test-connection", _ => Execute() },
                { "tc", _ => Execute() },
            };

        private void Execute()
        {
            _configWindow.IsOpen = true;
            var _ = new TickScheduler(() => _configWindow.TestConnection());
        }
    }
}
