using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;
using Pal.Common;

namespace Pal.Client.Database
{
    internal sealed class Cleanup
    {
        private readonly ILogger<Cleanup> _logger;
        private readonly IPalacePalConfiguration _configuration;

        public Cleanup(ILogger<Cleanup> logger, IPalacePalConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public void Purge(PalClientContext dbContext)
        {
            var toDelete = dbContext.Locations
                .Include(o => o.ImportedBy)
                .Include(o => o.RemoteEncounters)
                .AsSplitQuery()
                .Where(DefaultPredicate())
                .Where(AnyRemoteEncounter())
                .ToList();
            _logger.LogInformation("Cleaning up {Count} outdated locations", toDelete.Count);
            dbContext.Locations.RemoveRange(toDelete);
        }

        public void Purge(PalClientContext dbContext, ETerritoryType territoryType)
        {
            var toDelete = dbContext.Locations
                .Include(o => o.ImportedBy)
                .Include(o => o.RemoteEncounters)
                .AsSplitQuery()
                .Where(o => o.TerritoryType == (ushort)territoryType)
                .Where(DefaultPredicate())
                .Where(AnyRemoteEncounter())
                .ToList();
            _logger.LogInformation("Cleaning up {Count} outdated locations for territory {Territory}", toDelete.Count,
                territoryType);
            dbContext.Locations.RemoveRange(toDelete);
        }

        private Expression<Func<ClientLocation, bool>> DefaultPredicate()
        {
            return o => !o.Seen &&
                        o.ImportedBy.Count == 0 &&
                        o.Source != ClientLocation.ESource.SeenLocally &&
                        o.Source != ClientLocation.ESource.ExplodedLocally;
        }

        private Expression<Func<ClientLocation, bool>> AnyRemoteEncounter()
        {
            if (_configuration.Mode == EMode.Offline)
                return o => true;
            else
                // keep downloaded markers
                return o => o.Source != ClientLocation.ESource.Download;
        }
    }
}
