using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;
using Pal.Client.Database;
using Pal.Client.DependencyInjection;
using Pal.Client.Net;
using Pal.Client.Rendering;
using Pal.Client.Scheduled;
using Pal.Common;
using static Pal.Client.Rendering.SplatoonRenderer;

namespace Pal.Client.Floors
{
    internal sealed class FrameworkService : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FrameworkService> _logger;
        private readonly IFramework _framework;
        private readonly ConfigurationManager _configurationManager;
        private readonly IPalacePalConfiguration _configuration;
        private readonly IClientState _clientState;
        private readonly TerritoryState _territoryState;
        private readonly FloorService _floorService;
        private readonly DebugState _debugState;
        private readonly RenderAdapter _renderAdapter;
        private readonly IObjectTable _objectTable;
        private readonly RemoteApi _remoteApi;

        internal Queue<IQueueOnFrameworkThread> EarlyEventQueue { get; } = new();
        internal Queue<IQueueOnFrameworkThread> LateEventQueue { get; } = new();
        internal ConcurrentQueue<nint> NextUpdateObjects { get; } = new();

        public FrameworkService(
            IServiceProvider serviceProvider,
            ILogger<FrameworkService> logger,
            IFramework framework,
            ConfigurationManager configurationManager,
            IPalacePalConfiguration configuration,
            IClientState clientState,
            TerritoryState territoryState,
            FloorService floorService,
            DebugState debugState,
            RenderAdapter renderAdapter,
            IObjectTable objectTable,
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

        private void OnUpdate(object framework)
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
                    PluginLog.Debug($"PomanderOfSight is now set to inactive {_territoryState.PomanderOfSight}");
                    _territoryState.PomanderOfIntuition = PomanderState.Inactive;
                    PluginLog.Debug($"PomanderOfIntuition is now set to inactive {_territoryState.PomanderOfIntuition}");
                    recreateLayout = true;
                    _debugState.Reset();
                    Plugin.P._rootScope!.ServiceProvider.GetRequiredService<RenderAdapter>()._implementation.UpdateExitElement();
                    ExternalUtils.UpdateBronzeTreasureCoffers(_clientState.TerritoryType);
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
                e.Log();
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
            var playerPos = _clientState.LocalPlayer?.Position ?? Vector3.Zero;
            if (memoryTerritory is { Locations.Count: > 0 } &&
                (_configuration.DeepDungeons.Traps.OnlyVisibleAfterPomander ||
                 _configuration.DeepDungeons.HoardCoffers.OnlyVisibleAfterPomander))
            {
                try
                {
                    foreach (var location in memoryTerritory.Locations)
                    {
                        if (location.RenderElement == null || !location.RenderElement.IsValid)
                            return true;

                        uint desiredColor = DetermineColor(location, visibleLocations);
                        float desiredThickness = location.RenderElement.Thickness;

                        // scale color/thickness for smooth distance limiting
                        var dist = float.Abs(Vector3.Distance(location.Position, playerPos));
                        uint alpha = (desiredColor & 0xFF000000) >> 24;
                        alpha = ((uint)(alpha * float.Sqrt(1.0f - dist / P.Config.TrapHoardDistance))) << 24;
                        if (desiredColor != 0x00000000)
                            desiredColor = (desiredColor & 0x00FFFFFF) | alpha;
                        desiredThickness = float.Max(1.0f, (1.0f - dist / 60f) * 2.5f);

                        if (location.RenderElement.Color != desiredColor)
                        {
                            location.RenderElement.Color = desiredColor;
                            if (location.RenderElement2 != null)
                            {
                                location.RenderElement2.Color = desiredColor == RenderData.ColorInvisible? RenderData.ColorInvisible : (desiredColor.ToVector4() with { W = 50f / 255f }).ToUint();
                            }
                        }

                        if (location.RenderElement.Thickness != desiredThickness)
                        {
                            location.RenderElement.Thickness = desiredThickness;
                        }
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

            List<SplatoonElement> elements = new();
            foreach (var location in memoryTerritory.Locations)
            {
                if (location.Type == MemoryLocation.EType.Trap)
                {
                    CreateRenderElement(location, elements, DetermineColor(location, visibleMarkers), _configuration.DeepDungeons.Traps);
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

            List<SplatoonElement> elements = new();
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
                    return P.Config.TrapColor.ToUint();
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

        private void CreateRenderElement(MemoryLocation location, List<SplatoonElement> elements, uint color, MarkerConfiguration config)
        {
            var element = _renderAdapter.CreateElement(location.Type, location.Position, color, config.Fill);
            if(location.Type == MemoryLocation.EType.GoldCoffer)
            {
                /*{"Name":"Gold Treasure Coffer","type":1,"Enabled":false,"color":3355495679,"overlayBGColor":0,"overlayTextColor":4278242559,"overlayVOffset":0.6,"overlayFScale":1.3,"overlayText":" Gold Treasure Coffer","refActorPlaceholder":["<t>"],"refActorComparisonType":5,"includeOwnHitbox":true}

                {"Name":"Gold Treasure Coffer Fill","type":1,"Enabled":false,"color":838913279,"overlayVOffset":0.68,"overlayFScale":1.24,"refActorPlaceholder":["<t>"],"FillStep":0.429,"refActorComparisonType":5,"includeOwnHitbox":true,"Filled":true}
                */
                element.Delegate.color = color;
                if (P.Config.GoldText)
                {
                    element.Delegate.overlayBGColor = 0;
                    element.Delegate.overlayVOffset = 0.6f;
                    element.Delegate.overlayFScale = P.Config.OverlayFScale;
                    element.Delegate.overlayText = " Gold Treasure Coffer";
                    element.Delegate.overlayTextColor = color;
                }
                element.Delegate.radius = 1f;
                element.Delegate.Filled = false;

                var element2 = _renderAdapter.CreateElement(location.Type, location.Position, color);
                element2.Delegate.color = (color.ToVector4() with { W = 50f / 255f }).ToUint();
                element2.Delegate.radius = 1f;
                element2.Delegate.Filled = true;
                location.RenderElement2 = element2;
                if (config.Show && config.Fill)
                {
                    elements.Add(element2);
                }
            }
            else if(location.Type == MemoryLocation.EType.SilverCoffer)
            {
                /*
                 * {"Name":"Silver Treasure Coffer","type":1,"Enabled":false,"color":3372220415,"overlayBGColor":0,"overlayTextColor":4294967295,"overlayVOffset":0.6,"overlayFScale":1.3,"overlayText":" Silver Treasure Coffer","refActorType":1,"includeOwnHitbox":true}

                {"Name":"Silver Treasure Coffer Fill","type":1,"Enabled":false,"color":855638015,"overlayVOffset":0.68,"overlayFScale":1.24,"FillStep":0.429,"refActorType":1,"includeOwnHitbox":true,"Filled":true}

                 * */
                element.Delegate.color = color;
                if (P.Config.SilverText)
                {
                    element.Delegate.overlayBGColor = 0;
                    element.Delegate.overlayVOffset = 0.6f;
                    element.Delegate.overlayFScale = P.Config.OverlayFScale;
                    element.Delegate.overlayText = " Silver Treasure Coffer";
                    element.Delegate.overlayTextColor = color;
                }
                element.Delegate.radius = 1f;
                element.Delegate.Filled = false;

                var element2 = _renderAdapter.CreateElement(location.Type, location.Position, color);
                element2.Delegate.color = (color.ToVector4() with { W = 50f / 255f }).ToUint();
                element2.Delegate.radius = 1f;
                element2.Delegate.Filled = true;
                location.RenderElement2 = element2;
                if (config.Show && config.Fill)
                {
                    elements.Add(element2);
                }
            }
            else if(location.Type == MemoryLocation.EType.Trap)
            {
                //{"Name":"Mimic Trap Coffer","type":1,"Enabled":false,"color":4278190335,"overlayBGColor":0,"overlayTextColor":4278190335,"overlayVOffset":0.6,"overlayFScale":1.3,"overlayText":" Mimic Trap Coffer","refActorPlaceholder":["<t>"],"FillStep":0.029,"refActorComparisonType":5,"includeOwnHitbox":true,"AdditionalRotation":0.43633232}

                //{ "Name":"Mimic Trap Coffer Fill","type":1,"Enabled":false,"color":838861055,"overlayBGColor":0,"overlayTextColor":4278190335,"overlayVOffset":0.6,"overlayFScale":1.3,"refActorPlaceholder":["<t>"],"FillStep":0.029,"refActorComparisonType":5,"includeOwnHitbox":true,"AdditionalRotation":0.43633232,"Filled":true}

                element.Delegate.color = P.Config.TrapColor.ToUint();
                element.Delegate.Filled = false;

                var element2 = _renderAdapter.CreateElement(location.Type, location.Position, color);
                element2.Delegate.color = (P.Config.TrapColor with { W = 50f/255f }).ToUint();
                element2.Delegate.Filled = true;
                location.RenderElement2 = element2;
                if (config.Show && config.Fill)
                {
                    elements.Add(element2);
                }
            }
            location.RenderElement = element;

            if (config.Show)
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
