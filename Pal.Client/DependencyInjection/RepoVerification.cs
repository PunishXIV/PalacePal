using Dalamud.Game.Gui;
using Dalamud.Logging;
using Dalamud.Plugin;
using Microsoft.Extensions.Logging;
using Pal.Client.Extensions;
using Pal.Client.Properties;

namespace Pal.Client.DependencyInjection
{
    internal sealed class RepoVerification
    {
        public RepoVerification(ILogger<RepoVerification> logger, IDalamudPluginInterface pluginInterface, Chat chat)
        {
            logger.LogInformation("Install source: {Repo}", pluginInterface.SourceRepository);
            /*if (!pluginInterface.IsDev
                && !pluginInterface.SourceRepository.StartsWith("https://raw.githubusercontent.com/carvelli/")
                && !pluginInterface.SourceRepository.StartsWith("https://github.com/carvelli/"))
            {
                chat.Error(string.Format(Localization.Error_WrongRepository,
                    "https://github.com/carvelli/Dalamud-Plugins"));
                throw new InvalidOperationException();
            }*/
        }
    }
}
