using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using Pal.Client.Extensions;
using Pal.Client.Floors;
using Pal.Client.Floors.Tasks;
using Pal.Client.Net;
using Pal.Common;

namespace Pal.Client.Scheduled
{
    internal sealed class QueuedSyncResponse : IQueueOnFrameworkThread
    {
        public required SyncType Type { get; init; }
        public required ushort TerritoryType { get; init; }
        public required bool Success { get; init; }
        public required IReadOnlyList<PersistentLocation> Locations { get; init; }

        internal sealed class Handler : IQueueOnFrameworkThread.Handler<QueuedSyncResponse>
        {
            private readonly IServiceScopeFactory _serviceScopeFactory;
            private readonly IPalacePalConfiguration _configuration;
            private readonly FloorService _floorService;
            private readonly TerritoryState _territoryState;
            private readonly DebugState _debugState;

            public Handler(
                ILogger<Handler> logger,
                IServiceScopeFactory serviceScopeFactory,
                IPalacePalConfiguration configuration,
                FloorService floorService,
                TerritoryState territoryState,
                DebugState debugState)
                : base(logger)
            {
                _serviceScopeFactory = serviceScopeFactory;
                _configuration = configuration;
                _floorService = floorService;
                _territoryState = territoryState;
                _debugState = debugState;
            }

            protected override void Run(QueuedSyncResponse queued, ref bool recreateLayout)
            {
                recreateLayout = true;

                _logger.LogDebug(
                    "Sync response for territory {Territory} of type {Type}, success = {Success}, response objects = {Count}",
                    (ETerritoryType)queued.TerritoryType, queued.Type, queued.Success, queued.Locations.Count);
                var memoryTerritory = _floorService.GetTerritoryIfReady(queued.TerritoryType);
                if (memoryTerritory == null)
                {
                    _logger.LogWarning("Discarding sync response for territory {Territory} as it isn't ready",
                        (ETerritoryType)queued.TerritoryType);
                    return;
                }

                try
                {
                    var remoteMarkers = queued.Locations;
                    if (_configuration.Mode == EMode.Online && queued.Success && remoteMarkers.Count > 0)
                    {
                        switch (queued.Type)
                        {
                            case SyncType.Download:
                            case SyncType.Upload:
                                List<PersistentLocation> newLocations = new();
                                foreach (var remoteMarker in remoteMarkers)
                                {
                                    // Both uploads and downloads return the network id to be set, but only the downloaded marker is new as in to-be-saved.
                                    PersistentLocation? localLocation =
                                        memoryTerritory.Locations.SingleOrDefault(x => x == remoteMarker);
                                    if (localLocation != null)
                                    {
                                        localLocation.NetworkId = remoteMarker.NetworkId;
                                        continue;
                                    }

                                    if (queued.Type == SyncType.Download)
                                    {
                                        memoryTerritory.Locations.Add(remoteMarker);
                                        newLocations.Add(remoteMarker);
                                    }
                                }

                                if (newLocations.Count > 0)
                                    new SaveNewLocations(_serviceScopeFactory, memoryTerritory, newLocations).Start();

                                break;

                            case SyncType.MarkSeen:
                                var partialAccountId =
                                    _configuration.FindAccount(RemoteApi.RemoteUrl)?.AccountId.ToPartialId();
                                if (partialAccountId == null)
                                    break;

                                List<PersistentLocation> locationsToUpdate = new();
                                foreach (var remoteMarker in remoteMarkers)
                                {
                                    PersistentLocation? localLocation =
                                        memoryTerritory.Locations.SingleOrDefault(x => x == remoteMarker);
                                    if (localLocation != null)
                                    {
                                        localLocation.RemoteSeenOn.Add(partialAccountId);
                                        locationsToUpdate.Add(localLocation);
                                    }
                                }

                                if (locationsToUpdate.Count > 0)
                                {
                                    new MarkRemoteSeen(_serviceScopeFactory, memoryTerritory, locationsToUpdate,
                                        partialAccountId).Start();
                                }

                                break;
                        }
                    }

                    // don't modify state for outdated floors
                    if (_territoryState.LastTerritory != queued.TerritoryType)
                        return;

                    if (queued.Type == SyncType.Download)
                    {
                        if (queued.Success)
                            memoryTerritory.SyncState = ESyncState.Complete;
                        else
                            memoryTerritory.SyncState = ESyncState.Failed;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Sync failed for territory {Territory}", (ETerritoryType)queued.TerritoryType);
                    _debugState.SetFromException(e);
                    if (queued.Type == SyncType.Download)
                        memoryTerritory.SyncState = ESyncState.Failed;
                }
            }
        }
    }

    public enum ESyncState
    {
        NotAttempted,
        NotNeeded,
        Started,
        Complete,
        Failed,
    }

    public enum SyncType
    {
        Upload,
        Download,
        MarkSeen,
    }
}
