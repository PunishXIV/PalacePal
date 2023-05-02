using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pal.Client.Database;
using Pal.Client.Floors;
using Pal.Client.Floors.Tasks;
using Pal.Common;

namespace Pal.Client.DependencyInjection
{
    internal sealed class ImportService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly FloorService _floorService;
        private readonly Cleanup _cleanup;

        public ImportService(
            IServiceProvider serviceProvider,
            FloorService floorService,
            Cleanup cleanup)
        {
            _serviceProvider = serviceProvider;
            _floorService = floorService;
            _cleanup = cleanup;
        }

        public async Task<ImportHistory?> FindLast(CancellationToken token = default)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            await using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            return await dbContext.Imports.OrderByDescending(x => x.ImportedAt).ThenBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken: token);
        }

        public (int traps, int hoard) Import(ExportRoot import)
        {
            try
            {
                _floorService.SetToImportState();

                using var scope = _serviceProvider.CreateScope();
                using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

                dbContext.Imports.RemoveRange(dbContext.Imports.Where(x => x.RemoteUrl == import.ServerUrl).ToList());
                dbContext.SaveChanges();

                ImportHistory importHistory = new ImportHistory
                {
                    Id = Guid.Parse(import.ExportId),
                    RemoteUrl = import.ServerUrl,
                    ExportedAt = import.CreatedAt.ToDateTime(),
                    ImportedAt = DateTime.UtcNow,
                };
                dbContext.Imports.Add(importHistory);

                int traps = 0;
                int hoard = 0;
                foreach (var floor in import.Floors)
                {
                    ETerritoryType territoryType = (ETerritoryType)floor.TerritoryType;

                    List<PersistentLocation> existingLocations = dbContext.Locations
                        .Where(loc => loc.TerritoryType == floor.TerritoryType)
                        .ToList()
                        .Select(LoadTerritory.ToMemoryLocation)
                        .ToList();
                    foreach (var exportLocation in floor.Objects)
                    {
                        PersistentLocation persistentLocation = new PersistentLocation
                        {
                            Type = ToMemoryType(exportLocation.Type),
                            Position = new Vector3(exportLocation.X, exportLocation.Y, exportLocation.Z),
                            Source = ClientLocation.ESource.Unknown,
                        };

                        var existingLocation = existingLocations.FirstOrDefault(x => x == persistentLocation);
                        if (existingLocation != null)
                        {
                            var clientLoc = dbContext.Locations.FirstOrDefault(o => o.LocalId == existingLocation.LocalId);
                            clientLoc?.ImportedBy.Add(importHistory);

                            continue;
                        }

                        ClientLocation clientLocation = new ClientLocation
                        {
                            TerritoryType = (ushort)territoryType,
                            Type = ToClientLocationType(exportLocation.Type),
                            X = exportLocation.X,
                            Y = exportLocation.Y,
                            Z = exportLocation.Z,
                            Seen = false,
                            Source = ClientLocation.ESource.Import,
                            ImportedBy = new List<ImportHistory> { importHistory },
                            SinceVersion = typeof(Plugin).Assembly.GetName().Version!.ToString(2),
                        };
                        dbContext.Locations.Add(clientLocation);

                        if (exportLocation.Type == ExportObjectType.Trap)
                            traps++;
                        else if (exportLocation.Type == ExportObjectType.Hoard)
                            hoard++;
                    }
                }

                dbContext.SaveChanges();

                _cleanup.Purge(dbContext);
                dbContext.SaveChanges();

                return (traps, hoard);
            }
            finally
            {
                _floorService.ResetAll();
            }
        }

        private MemoryLocation.EType ToMemoryType(ExportObjectType exportLocationType)
        {
            return exportLocationType switch
            {
                ExportObjectType.Trap => MemoryLocation.EType.Trap,
                ExportObjectType.Hoard => MemoryLocation.EType.Hoard,
                _ => throw new ArgumentOutOfRangeException(nameof(exportLocationType), exportLocationType, null)
            };
        }

        private ClientLocation.EType ToClientLocationType(ExportObjectType exportLocationType)
        {
            return exportLocationType switch
            {
                ExportObjectType.Trap => ClientLocation.EType.Trap,
                ExportObjectType.Hoard => ClientLocation.EType.Hoard,
                _ => throw new ArgumentOutOfRangeException(nameof(exportLocationType), exportLocationType, null)
            };
        }

        public void RemoveById(Guid id)
        {
            try
            {
                _floorService.SetToImportState();
                using var scope = _serviceProvider.CreateScope();
                using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

                dbContext.RemoveRange(dbContext.Imports.Where(x => x.Id == id));
                dbContext.SaveChanges();

                _cleanup.Purge(dbContext);
                dbContext.SaveChanges();
            }
            finally
            {
                _floorService.ResetAll();
            }
        }
    }
}
