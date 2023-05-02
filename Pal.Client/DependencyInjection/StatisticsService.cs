using System;
using System.Threading.Tasks;
using Dalamud.Game.Gui;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;
using Pal.Client.Extensions;
using Pal.Client.Net;
using Pal.Client.Properties;
using Pal.Client.Windows;

namespace Pal.Client.DependencyInjection
{
    internal sealed class StatisticsService
    {
        private readonly IPalacePalConfiguration _configuration;
        private readonly ILogger<StatisticsService> _logger;
        private readonly RemoteApi _remoteApi;
        private readonly StatisticsWindow _statisticsWindow;
        private readonly Chat _chat;

        public StatisticsService(
            IPalacePalConfiguration configuration,
            ILogger<StatisticsService> logger,
            RemoteApi remoteApi,
            StatisticsWindow statisticsWindow,
            Chat chat)
        {
            _configuration = configuration;
            _logger = logger;
            _remoteApi = remoteApi;
            _statisticsWindow = statisticsWindow;
            _chat = chat;
        }

        public void ShowGlobalStatistics()
        {
            Task.Run(async () => await FetchFloorStatistics());
        }

        private async Task FetchFloorStatistics()
        {
            try
            {
                if (!_configuration.HasRoleOnCurrentServer(RemoteApi.RemoteUrl, "statistics:view"))
                {
                    _chat.Error(Localization.Command_pal_stats_CurrentFloor);
                    return;
                }

                var (success, floorStatistics) = await _remoteApi.FetchStatistics();
                if (success)
                {
                    _statisticsWindow.SetFloorData(floorStatistics);
                    _statisticsWindow.IsOpen = true;
                }
                else
                {
                    _chat.Error(Localization.Command_pal_stats_UnableToFetchStatistics);
                }
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.PermissionDenied)
            {
                _logger.LogWarning(e, "Access denied while fetching floor statistics");
                _chat.Error(Localization.Command_pal_stats_CurrentFloor);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not fetch floor statistics");
                _chat.Error(string.Format(Localization.Error_CommandFailed,
                    $"{e.GetType()} - {e.Message}"));
            }
        }
    }
}
