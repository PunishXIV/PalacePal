using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;
using Pal.Client.Database;
using Pal.Client.DependencyInjection;
using Pal.Client.Net;
using Pal.Client.Rendering;
using Pal.Client.Scheduled;
using Pal.Common;

namespace Pal.Client.Floors
{
    internal sealed class FrameworkService : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FrameworkService> _logger;
        private readonly Framework _framework;
        private readonly ConfigurationManager _configurationManager;
        private readonly IPalacePalConfiguration _configuration;
        private readonly ClientState _clientState;
        private readonly TerritoryState _territoryState;
        private readonly FloorService _floorService;
        private readonly DebugState _debugState;
        private readonly RenderAdapter _renderAdapter;
        private readonly ObjectTable _objectTable;
        private readonly RemoteApi _remoteApi;

        internal Queue<IQueueOnFrameworkThread> EarlyEventQueue { get; } = new();
        internal Queue<IQueueOnFrameworkThread> LateEventQueue { get; } = new();
        internal ConcurrentQueue<nint> NextUpdateObjects { get; } = new();

        public FrameworkService(
            IServiceProvider serviceProvider,
            ILogger<FrameworkService> logger,
            Framework framework,
            ConfigurationManager configurationManager,
            IPalacePalConfiguration configuration,
            ClientState clientState,
            TerritoryState territoryState,
            FloorService floorService,
            DebugState debugState,
            RenderAdapter renderAdapter,
            ObjectTable objectTable,
            RemoteApi remoteApi)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _framework = framework;
            _configurationManager = configurationManager;
            _configuration = configuration;
            _clientState = clientState;
            _territoryState = territoryState;
            _floorService = floorService;
            _debugState = debugState;
            _renderAdapter = renderAdapter;
            _objectTable = objectTable;
            _remoteApi = remoteApi;

            _framework.Update += OnUpdate;
            _configurationManager.Saved += OnSaved;
        }

        public void Dispose()
        {
            _framework.Update -= OnUpdate;
            _configurationManager.Saved -= OnSaved;
        }

        private void OnSaved(object? sender, IPalacePalConfiguration? config)
            => EarlyEventQueue.Enqueue(new QueuedConfigUpdate());

        private void OnUpdate(Framework framework)
        {
            if (_configuration.FirstUse)
                return;

            try
            {
                bool recreateLayout = false;

                while (EarlyEventQueue.TryDequeue(out IQueueOnFrameworkThread? queued))
                    HandleQueued(queued, ref recreateLayout);

                if (_territoryState.LastTerritory != _clientState.TerritoryType)
                {
                    MemoryTerritory? oldTerritory = _floorService.GetTerritoryIfReady(_territoryState.LastTerritory);
                    if (oldTerritory != null)
                        oldTerritory.SyncState = ESyncState.NotAttempted;

                    _territoryState.LastTerritory = _clientState.TerritoryType;
                    NextUpdateObjects.Clear();

                    _floorService.ChangeTerritory(_territoryState.LastTerritory);
                    _territoryState.PomanderOfSight = PomanderState.Inactive;
                    _territoryState.PomanderOfIntuition = PomanderState.Inactive;
                    recreateLayout = true;
                    _debugState.Reset();
                }

                if (!_territoryState.IsInDeepDungeon() || !_floorService.IsReady(_territoryState.LastTerritory))
                    return;

                if (_renderAdapter.RequireRedraw)
                {
                    recreateLayout = true;
                    _renderAdapter.RequireRedraw = false;
                }

                ETerritoryType territoryType = (ETerritoryType)_territoryState.LastTerritory;
                MemoryTerritory memoryTerritory = _floorService.GetTerritoryIfReady(territoryType)!;
                if (_configuration.Mode == EMode.Online && memoryTerritory.SyncState == ESyncState.NotAttempted)
                {
                    memoryTerritory.SyncState = ESyncState.Started;
                    Task.Run(async () => await DownloadLocationsForTerritory(_territoryState.LastTerritory));
                }

                while (LateEventQueue.TryDequeue(out IQueueOnFrameworkThread? queued))
                    HandleQueued(queued, ref recreateLayout);

                (IReadOnlyList<PersistentLocation> visiblePersistentMarkers,
                        IReadOnlyList<EphemeralLocation> visibleEphemeralMarkers) =
                    GetRelevantGameObjects();

                HandlePersistentLocations(territoryType, visiblePersistentMarkers, recreateLayout);

                if (_floorService.MergeEphemeralLocations(visibleEphemeralMarkers, recreateLayout))
                    RecreateEphemeralLayout();
            }
            catch (Exception e)
            {
                _debugState.SetFromException(e);
            }
        }

        #region Render Markers

        private void HandlePersistentLocations(ETerritoryType territoryType,
            IReadOnlyList<PersistentLocation> visiblePersistentMarkers,
            bool recreateLayout)
        {
            bool recreatePersistentLocations = _floorService.MergePersistentLocations(
                territoryType,
                visiblePersistentMarkers,
                recreateLayout,
                out List<PersistentLocation> locationsToSync);
            recreatePersistentLocations |= CheckLocationsForPomanders(visiblePersistentMarkers);
            if (locationsToSync.Count > 0)
            {
                Task.Run(async () =>
                    await SyncSeenMarkersForTerritory(_territoryState.LastTerritory, locationsToSync));
            }

            UploadLocations();

            if (recreatePersistentLocations)
                RecreatePersistentLayout(visiblePersistentMarkers);
        }

        private bool CheckLocationsForPomanders(IReadOnlyList<PersistentLocation> visibleLocations)
        {
            MemoryTerritory? memoryTerritory = _floorService.GetTerritoryIfReady(_territoryState.LastTerritory);
            if (memoryTerritory is { Locations.Count: > 0 } &&
                (_configuration.DeepDungeons.Traps.OnlyVisibleAfterPomander ||
                 _configuration.DeepDungeons.HoardCoffers.OnlyVisibleAfterPomander))
            {
                try
                {
                    foreach (var location in memoryTerritory.Locations)
                    {
                        uint desiredColor = DetermineColor(location, visibleLocations);
                        if (location.RenderElement == null || !location.RenderElement.IsValid)
                            return true;

                        if (location.RenderElement.Color != desiredColor)
                            location.RenderElement.Color = desiredColor;
                    }
                }
                catch (Exception e)
                {
                    _debugState.SetFromException(e);
                    return true;
                }
            }

            return false;
        }

        private void UploadLocations()
        {
            MemoryTerritory? memoryTerritory = _floorService.GetTerritoryIfReady(_territoryState.LastTerritory);
            if (memoryTerritory == null || memoryTerritory.SyncState != ESyncState.Complete)
                return;

            List<PersistentLocation> locationsToUpload = memoryTerritory.Locations
                .Where(loc => loc.NetworkId == null && loc.UploadRequested == false)
                .ToList();
            if (locationsToUpload.Count > 0)
            {
                foreach (var location in locationsToUpload)
                    location.UploadRequested = true;

                Task.Run(async () =>
                    await UploadLocationsForTerritory(_territoryState.LastTerritory, locationsToUpload));
            }
        }

        private void RecreatePersistentLayout(IReadOnlyList<PersistentLocation> visibleMarkers)
        {
            _renderAdapter.ResetLayer(ELayer.TrapHoard);

            MemoryTerritory? memoryTerritory = _floorService.GetTerritoryIfReady(_territoryState.LastTerritory);
            if (memoryTerritory == null)
                return;

            List<IRenderElement> elements = new();
            foreach (var location in memoryTerritory.Locations)
            {
                if (location.Type == MemoryLocation.EType.Trap)
                {
                    CreateRenderElement(location, elements, DetermineColor(location, visibleMarkers),
                        _configuration.DeepDungeons.Traps);
                }
                else if (location.Type == MemoryLocation.EType.Hoard)
                {
                    CreateRenderElement(location, elements, DetermineColor(location, visibleMarkers),
                        _configuration.DeepDungeons.HoardCoffers);
                }
            }

            if (elements.Count == 0)
                return;

            _renderAdapter.SetLayer(ELayer.TrapHoard, elements);
        }

        private void RecreateEphemeralLayout()
        {
            _renderAdapter.ResetLayer(ELayer.RegularCoffers);

            List<IRenderElement> elements = new();
            foreach (var location in _floorService.EphemeralLocations)
            {
                if (location.Type == MemoryLocation.EType.SilverCoffer &&
                    _configuration.DeepDungeons.SilverCoffers.Show)
                {
                    CreateRenderElement(location, elements, DetermineColor(location),
                        _configuration.DeepDungeons.SilverCoffers);
                }
                else if (location.Type == MemoryLocation.EType.GoldCoffer &&
                      _configuration.DeepDungeons.GoldCoffers.Show)
                {
                    CreateRenderElement(location, elements, DetermineColor(location),
                        _configuration.DeepDungeons.GoldCoffers);
                }
            }

            if (elements.Count == 0)
                return;

            _renderAdapter.SetLayer(ELayer.RegularCoffers, elements);
        }

        private uint DetermineColor(PersistentLocation location, IReadOnlyList<PersistentLocation> visibleLocations)
        {
            switch (location.Type)
            {
                case MemoryLocation.EType.Trap
                    when _territoryState.PomanderOfSight == PomanderState.Inactive ||
                         !_configuration.DeepDungeons.Traps.OnlyVisibleAfterPomander ||
                         visibleLocations.Any(x => x == location):
                    return _configuration.DeepDungeons.Traps.Color;
                case MemoryLocation.EType.Hoard
                    when _territoryState.PomanderOfIntuition == PomanderState.Inactive ||
                         !_configuration.DeepDungeons.HoardCoffers.OnlyVisibleAfterPomander ||
                         visibleLocations.Any(x => x == location):
                    return _configuration.DeepDungeons.HoardCoffers.Color;
                default:
                    return RenderData.ColorInvisible;
            }
        }

        private uint DetermineColor(EphemeralLocation location)
        {
            return location.Type switch
            {
                MemoryLocation.EType.SilverCoffer => _configuration.DeepDungeons.SilverCoffers.Color,
                MemoryLocation.EType.GoldCoffer => _configuration.DeepDungeons.GoldCoffers.Color,
                _ => RenderData.ColorInvisible
            };
        }

        private void CreateRenderElement(MemoryLocation location, List<IRenderElement> elements, uint color,
            MarkerConfiguration config)
        {
            if (!config.Show)
                return;

            var element = _renderAdapter.CreateElement(location.Type, location.Position, color, config.Fill);
            location.RenderElement = element;
            elements.Add(element);
        }

        #endregion

        #region Up-/Download

        private async Task DownloadLocationsForTerritory(ushort territoryId)
        {
            try
            {
                _logger.LogInformation("Downloading territory {Territory} from server", (ETerritoryType)territoryId);
                var (success, downloadedMarkers) = await _remoteApi.DownloadRemoteMarkers(territoryId);
                LateEventQueue.Enqueue(new QueuedSyncResponse
                {
                    Type = SyncType.Download,
                    TerritoryType = territoryId,
                    Success = success,
                    Locations = downloadedMarkers
                });
            }
            catch (Exception e)
            {
                _debugState.SetFromException(e);
            }
        }

        private async Task UploadLocationsForTerritory(ushort territoryId, List<PersistentLocation> locationsToUpload)
        {
            try
            {
                _logger.LogInformation("Uploading {Count} locations for territory {Territory} to server",
                    locationsToUpload.Count, (ETerritoryType)territoryId);
                var (success, uploadedLocations) = await _remoteApi.UploadLocations(territoryId, locationsToUpload);
                LateEventQueue.Enqueue(new QueuedSyncResponse
                {
                    Type = SyncType.Upload,
                    TerritoryType = territoryId,
                    Success = success,
                    Locations = uploadedLocations
                });
            }
            catch (Exception e)
            {
                _debugState.SetFromException(e);
            }
        }

        private async Task SyncSeenMarkersForTerritory(ushort territoryId,
            IReadOnlyList<PersistentLocation> locationsToUpdate)
        {
            try
            {
                _logger.LogInformation("Syncing {Count} seen locations for territory {Territory} to server",
                    locationsToUpdate.Count, (ETerritoryType)territoryId);
                var success = await _remoteApi.MarkAsSeen(territoryId, locationsToUpdate);
                LateEventQueue.Enqueue(new QueuedSyncResponse
                {
                    Type = SyncType.MarkSeen,
                    TerritoryType = territoryId,
                    Success = success,
                    Locations = locationsToUpdate,
                });
            }
            catch (Exception e)
            {
                _debugState.SetFromException(e);
            }
        }

        #endregion

        private (IReadOnlyList<PersistentLocation>, IReadOnlyList<EphemeralLocation>) GetRelevantGameObjects()
        {
            List<PersistentLocation> persistentLocations = new();
            List<EphemeralLocation> ephemeralLocations = new();
            for (int i = 246; i < _objectTable.Length; i++)
            {
                GameObject? obj = _objectTable[i];
                if (obj == null)
                    continue;

                switch ((uint)Marshal.ReadInt32(obj.Address + 128))
                {
                    case 2007182:
                    case 2007183:
                    case 2007184:
                    case 2007185:
                    case 2007186:
                    case 2009504:
                        persistentLocations.Add(new PersistentLocation
                        {
                            Type = MemoryLocation.EType.Trap,
                            Position = obj.Position,
                            Seen = true,
                            Source = ClientLocation.ESource.SeenLocally,
                        });
                        break;

                    case 2007542:
                    case 2007543:
                        persistentLocations.Add(new PersistentLocation
                        {
                            Type = MemoryLocation.EType.Hoard,
                            Position = obj.Position,
                            Seen = true,
                            Source = ClientLocation.ESource.SeenLocally,
                        });
                        break;

                    case 2007357:
                        ephemeralLocations.Add(new EphemeralLocation
                        {
                            Type = MemoryLocation.EType.SilverCoffer,
                            Position = obj.Position,
                            Seen = true,
                        });
                        break;

                    case 2007358:
                        ephemeralLocations.Add(new EphemeralLocation
                        {
                            Type = MemoryLocation.EType.GoldCoffer,
                            Position = obj.Position,
                            Seen = true
                        });
                        break;
                }
            }

            while (NextUpdateObjects.TryDequeue(out nint address))
            {
                var obj = _objectTable.FirstOrDefault(x => x.Address == address);
                if (obj != null && obj.Position.Length() > 0.1)
                {
                    persistentLocations.Add(new PersistentLocation
                    {
                        Type = MemoryLocation.EType.Trap,
                        Position = obj.Position,
                        Seen = true,
                        Source = ClientLocation.ESource.ExplodedLocally,

                    });
                }
            }

            return (persistentLocations, ephemeralLocations);
        }

        private void HandleQueued(IQueueOnFrameworkThread queued, ref bool recreateLayout)
        {
            Type handlerType = typeof(IQueueOnFrameworkThread.Handler<>).MakeGenericType(queued.GetType());
            var handler = (IQueueOnFrameworkThread.IHandler)_serviceProvider.GetRequiredService(handlerType);

            handler.RunIfCompatible(queued, ref recreateLayout);
        }
    }
}
