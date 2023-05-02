using System;
using System.Collections.Generic;
using Pal.Client.DependencyInjection;

namespace Pal.Client.Commands
{
    internal sealed class PalStatsCommand : ISubCommand
    {
        private readonly StatisticsService _statisticsService;

        public PalStatsCommand(StatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        public IReadOnlyDictionary<string, Action<string>> GetHandlers()
            => new Dictionary<string, Action<string>>
            {
                { "stats", _ => Execute() },
            };

        private void Execute()
            => _statisticsService.ShowGlobalStatistics();
    }
}
