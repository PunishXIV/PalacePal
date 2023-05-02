using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Database;
using Pal.Common;

namespace Pal.Client.Configuration.Legacy
{
    /// <summary>
    /// Imports legacy territoryType.json files into the database if it exists, and no markers for that territory exist.
    /// </summary>
    internal sealed class JsonMigration
    {
        private readonly ILogger<JsonMigration> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly DalamudPluginInterface _pluginInterface;

        public JsonMigration(ILogger<JsonMigration> logger, IServiceScopeFactory serviceScopeFactory,
            DalamudPluginInterface pluginInterface)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _pluginInterface = pluginInterface;
        }

#pragma warning disable CS0612
        public async Task MigrateAsync(CancellationToken cancellationToken)
        {
            List<JsonFloorState> floorsToMigrate = new();
            JsonFloorState.ForEach(floorsToMigrate.Add);

            if (floorsToMigrate.Count == 0)
            {
                _logger.LogInformation("Found no floors to migrate");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            await using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            var fileStream = new FileStream(
                Path.Join(_pluginInterface.GetPluginConfigDirectory(),
                    $"territory-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip"),
                FileMode.CreateNew);
            using (var backup = new ZipArchive(fileStream, ZipArchiveMode.Create, false))
            {
                IReadOnlyDictionary<Guid, ImportHistory> imports =
                    await dbContext.Imports.ToDictionaryAsync(import => import.Id, cancellationToken);

                foreach (var floorToMigrate in floorsToMigrate)
                {
                    backup.CreateEntryFromFile(floorToMigrate.GetSaveLocation(),
                        Path.GetFileName(floorToMigrate.GetSaveLocation()), CompressionLevel.SmallestSize);
                    await MigrateFloor(dbContext, floorToMigrate, imports, cancellationToken);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Removing {Count} old json files", floorsToMigrate.Count);
            foreach (var floorToMigrate in floorsToMigrate)
                File.Delete(floorToMigrate.GetSaveLocation());
        }

        /// <returns>Whether to archive this file once complete</returns>
        private async Task MigrateFloor(
            PalClientContext dbContext,
            JsonFloorState floorToMigrate,
            IReadOnlyDictionary<Guid, ImportHistory> imports,
            CancellationToken cancellationToken)
        {
            using var logScope = _logger.BeginScope($"Import {(ETerritoryType)floorToMigrate.TerritoryType}");
            if (floorToMigrate.Markers.Count == 0)
            {
                _logger.LogInformation("Skipping migration, floor has no markers");
            }

            if (await dbContext.Locations.AnyAsync(o => o.TerritoryType == floorToMigrate.TerritoryType,
                    cancellationToken))
            {
                _logger.LogInformation("Skipping migration, floor already has locations in the database");
                return;
            }

            _logger.LogInformation("Starting migration of {Count} locations", floorToMigrate.Markers.Count);
            List<ClientLocation> clientLocations = floorToMigrate.Markers
                .Where(o => o.Type == JsonMarker.EType.Trap || o.Type == JsonMarker.EType.Hoard)
                .Select(o =>
                {
                    var clientLocation = new ClientLocation
                    {
                        TerritoryType = floorToMigrate.TerritoryType,
                        Type = MapJsonType(o.Type),
                        X = o.Position.X,
                        Y = o.Position.Y,
                        Z = o.Position.Z,
                        Seen = o.Seen,

                        // the SelectMany is misleading here, each import has either 0 or 1 associated db entry with that id
                        ImportedBy = o.Imports
                            .Select(importId =>
                                imports.TryGetValue(importId, out ImportHistory? import) ? import : null)
                            .Where(import => import != null)
                            .Cast<ImportHistory>()
                            .Distinct()
                            .ToList(),

                        // if we have a location not encountered locally, which also wasn't imported,
                        // it very likely is a download (but we have no information to track this).
                        Source = o.Seen ? ClientLocation.ESource.SeenLocally :
                            o.Imports.Count > 0 ? ClientLocation.ESource.Import : ClientLocation.ESource.Download,
                        SinceVersion = o.SinceVersion ?? "0.0",
                    };

                    clientLocation.RemoteEncounters = o.RemoteSeenOn
                        .Select(accountId => new RemoteEncounter(clientLocation, accountId))
                        .ToList();

                    return clientLocation;
                }).ToList();
            await dbContext.Locations.AddRangeAsync(clientLocations, cancellationToken);

            _logger.LogInformation("Migrated {Count} locations", clientLocations.Count);
        }

        private ClientLocation.EType MapJsonType(JsonMarker.EType type)
        {
            return type switch
            {
                JsonMarker.EType.Trap => ClientLocation.EType.Trap,
                JsonMarker.EType.Hoard => ClientLocation.EType.Hoard,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
#pragma warning restore CS0612
    }
}
