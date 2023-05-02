using System;
using System.Collections.Generic;
using Pal.Client.Configuration;
using Pal.Client.Windows;

namespace Pal.Client.Commands
{
    internal class PalConfigCommand : ISubCommand
    {
        private readonly IPalacePalConfiguration _configuration;
        private readonly AgreementWindow _agreementWindow;
        private readonly ConfigWindow _configWindow;

        public PalConfigCommand(
            IPalacePalConfiguration configuration,
            AgreementWindow agreementWindow,
            ConfigWindow configWindow)
        {
            _configuration = configuration;
            _agreementWindow = agreementWindow;
            _configWindow = configWindow;
        }


        public IReadOnlyDictionary<string, Action<string>> GetHandlers()
            => new Dictionary<string, Action<string>>
            {
                { "config", _ => Execute() },
                { "", _ => Execute() }
            };

        public void Execute()
        {
            if (_configuration.FirstUse)
                _agreementWindow.IsOpen = true;
            else
                _configWindow.Toggle();
        }
    }
}
