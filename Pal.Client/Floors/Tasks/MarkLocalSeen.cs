using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Database;

namespace Pal.Client.Floors.Tasks
{
    internal sealed class MarkLocalSeen : DbTask<MarkLocalSeen>
    {
        private readonly MemoryTerritory _territory;
        private readonly IReadOnlyList<PersistentLocation> _locations;

        public MarkLocalSeen(IServiceScopeFactory serviceScopeFactory, MemoryTerritory territory,
            IReadOnlyList<PersistentLocation> locations)
            : base(serviceScopeFactory)
        {
            _territory = territory;
            _locations = locations;
        }

        protected override void Run(PalClientContext dbContext, ILogger<MarkLocalSeen> logger)
        {
            lock (_territory.LockObj)
            {
                logger.LogInformation("Marking {Count} locations as seen locally in territory {Territory}", _locations.Count,
                    _territory.TerritoryType);
                List<int> localIds = _locations.Select(l => l.LocalId).Where(x => x != null).Cast<int>().ToList();
                dbContext.Locations
                    .Where(loc => localIds.Contains(loc.LocalId))
                    .ExecuteUpdate(loc => loc.SetProperty(l => l.Seen, true));
                dbContext.SaveChanges();
            }
        }
    }
}
