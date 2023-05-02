using Account;
using Pal.Common;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Database;
using Pal.Client.DependencyInjection;
using Pal.Client.Properties;
using Pal.Client.Windows;

namespace Pal.Client.Scheduled
{
    internal sealed class QueuedImport : IQueueOnFrameworkThread
    {
        private ExportRoot Export { get; }
        private Guid ExportId { get; set; }
        private int ImportedTraps { get; set; }
        private int ImportedHoardCoffers { get; set; }

        public QueuedImport(string sourcePath)
        {
            using var input = File.OpenRead(sourcePath);
            Export = ExportRoot.Parser.ParseFrom(input);
        }

        internal sealed class Handler : IQueueOnFrameworkThread.Handler<QueuedImport>
        {
            private readonly IServiceScopeFactory _serviceScopeFactory;
            private readonly Chat _chat;
            private readonly ImportService _importService;
            private readonly ConfigWindow _configWindow;

            public Handler(
                ILogger<Handler> logger,
                IServiceScopeFactory serviceScopeFactory,
                Chat chat,
                ImportService importService,
                ConfigWindow configWindow)
                : base(logger)
            {
                _serviceScopeFactory = serviceScopeFactory;
                _chat = chat;
                _importService = importService;
                _configWindow = configWindow;
            }

            protected override void Run(QueuedImport import, ref bool recreateLayout)
            {
                recreateLayout = true;

                try
                {
                    if (!Validate(import))
                        return;

                    Task.Run(() =>
                    {
                        try
                        {
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();
                                (import.ImportedTraps, import.ImportedHoardCoffers) =
                                    _importService.Import(import.Export);
                            }

                            _configWindow.UpdateLastImport();

                            _logger.LogInformation(
                                "Imported {ExportId} for {Traps} traps, {Hoard} hoard coffers", import.ExportId,
                                import.ImportedTraps, import.ImportedHoardCoffers);
                            _chat.Message(string.Format(Localization.ImportCompleteStatistics, import.ImportedTraps,
                                import.ImportedHoardCoffers));
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Import failed in inner task");
                            _chat.Error(string.Format(Localization.Error_ImportFailed, e));
                        }
                    });
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Import failed");
                    _chat.Error(string.Format(Localization.Error_ImportFailed, e));
                }
            }

            private bool Validate(QueuedImport import)
            {
                if (import.Export.ExportVersion != ExportConfig.ExportVersion)
                {
                    _logger.LogError(
                        "Import: Different version in export file, {ExportVersion} != {ConfiguredVersion}",
                        import.Export.ExportVersion, ExportConfig.ExportVersion);
                    _chat.Error(Localization.Error_ImportFailed_IncompatibleVersion);
                    return false;
                }

                if (!Guid.TryParse(import.Export.ExportId, out Guid exportId) || exportId == Guid.Empty)
                {
                    _logger.LogError("Import: Invalid export id '{Id}'", import.Export.ExportId);
                    _chat.Error(Localization.Error_ImportFailed_InvalidFile);
                    return false;
                }

                import.ExportId = exportId;

                if (string.IsNullOrEmpty(import.Export.ServerUrl))
                {
                    // If we allow for backups as import/export, this should be removed
                    _logger.LogError("Import: No server URL");
                    _chat.Error(Localization.Error_ImportFailed_InvalidFile);
                    return false;
                }

                return true;
            }
        }
    }
}
