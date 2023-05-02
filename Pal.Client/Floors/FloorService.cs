using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Pal.Client.Configuration;
using Pal.Client.Database;
using Pal.Client.Extensions;
using Pal.Client.Floors.Tasks;
using Pal.Client.Net;
using Pal.Common;

namespace Pal.Client.Floors
{
    internal sealed class FloorService
    {
        private readonly IPalacePalConfiguration _configuration;
        private readonly Cleanup _cleanup;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IReadOnlyDictionary<ETerritoryType, MemoryTerritory> _territories;

        private ConcurrentBag<EphemeralLocation> _ephemeralLocations = new();

        public FloorService(IPalacePalConfiguration configuration, Cleanup cleanup,
            IServiceScopeFactory serviceScopeFactory)
        {
            _configuration = configuration;
            _cleanup = cleanup;
            _serviceScopeFactory = serviceScopeFactory;
            _territories = Enum.GetValues<ETerritoryType>().ToDictionary(o => o, o => new MemoryTerritory(o));
        }

        public IReadOnlyCollection<EphemeralLocation> EphemeralLocations => _ephemeralLocations;
        public bool IsImportRunning { get; private set; }

        public void ChangeTerritory(ushort territoryType)
        {
            _ephemeralLocations = new ConcurrentBag<EphemeralLocation>();

            if (typeof(ETerritoryType).IsEnumDefined(territoryType))
                ChangeTerritory((ETerritoryType)territoryType);
        }

        private void ChangeTerritory(ETerritoryType newTerritory)
        {
            var territory = _territories[newTerritory];
            if (territory.ReadyState == MemoryTerritory.EReadyState.NotLoaded)
            {
                territory.ReadyState = MemoryTerritory.EReadyState.Loading;
                new LoadTerritory(_serviceScopeFactory, _cleanup, territory).Start();
            }
        }

        public MemoryTerritory? GetTerritoryIfReady(ushort territoryType)
        {
            if (typeof(ETerritoryType).IsEnumDefined(territoryType))
                return GetTerritoryIfReady((ETerritoryType)territoryType);

            return null;
        }

        public MemoryTerritory? GetTerritoryIfReady(ETerritoryType territoryType)
        {
            var territory = _territories[territoryType];
            if (territory.ReadyState != MemoryTerritory.EReadyState.Ready)
                return null;

            return territory;
        }

        public bool IsReady(ushort territoryId) => GetTerritoryIfReady(territoryId) != null;

        public bool MergePersistentLocations(
            ETerritoryType territoryType,
            IReadOnlyList<PersistentLocation> visibleLocations,
            bool recreateLayout,
            out List<PersistentLocation> locationsToSync)
        {
            MemoryTerritory? territory = GetTerritoryIfReady(territoryType);
            locationsToSync = new();
            if (territory == null)
                return false;

            var partialAccountId = _configuration.FindAccount(RemoteApi.RemoteUrl)?.AccountId.ToPartialId();
            var persistentLocations = territory.Locations.ToList();

            List<PersistentLocation> markAsSeen = new();
            List<PersistentLocation> newLocations = new();
            foreach (var visibleLocation in visibleLocations)
            {
                PersistentLocation? existingLocation = persistentLocations.SingleOrDefault(x => x == visibleLocation);
                if (existingLocation != null)
                {
                    if (existingLocation is { Seen: false, LocalId: { } })
                    {
                        existingLocation.Seen = true;
                        markAsSeen.Add(existingLocation);
                    }

                    // This requires you to have seen a trap/hoard marker once per floor to synchronize this for older local states,
                    // markers discovered afterwards are automatically marked seen.
                    if (partialAccountId != null &&
                        existingLocation is { LocalId: { }, NetworkId: { }, RemoteSeenRequested: false } &&
                        !existingLocation.RemoteSeenOn.Contains(partialAccountId))
                    {
                        existingLocation.RemoteSeenRequested = true;
                        locationsToSync.Add(existingLocation);
                    }

                    continue;
                }

                territory.Locations.Add(visibleLocation);
                newLocations.Add(visibleLocation);
                recreateLayout = true;
            }

            if (markAsSeen.Count > 0)
                new MarkLocalSeen(_serviceScopeFactory, territory, markAsSeen).Start();

            if (newLocations.Count > 0)
                new SaveNewLocations(_serviceScopeFactory, territory, newLocations).Start();

            return recreateLayout;
        }

        /// <returns>Whether the locations have changed</returns>
        public bool MergeEphemeralLocations(IReadOnlyList<EphemeralLocation> visibleLocations, bool recreate)
        {
            recreate |= _ephemeralLocations.Any(loc => visibleLocations.All(x => x != loc));
            recreate |= visibleLocations.Any(loc => _ephemeralLocations.All(x => x != loc));

            if (!recreate)
                return false;

            _ephemeralLocations.Clear();
            foreach (var visibleLocation in visibleLocations)
                _ephemeralLocations.Add(visibleLocation);

            return true;
        }

        public void ResetAll()
        {
            IsImportRunning = false;
            foreach (var memoryTerritory in _territories.Values)
            {
                lock (memoryTerritory.LockObj)
                    memoryTerritory.Reset();
            }
        }

        public void SetToImportState()
        {
            IsImportRunning = true;
            foreach (var memoryTerritory in _territories.Values)
            {
                lock (memoryTerritory.LockObj)
                    memoryTerritory.ReadyState = MemoryTerritory.EReadyState.Importing;
            }
        }
    }
}
