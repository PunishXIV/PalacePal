using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState;
using Dalamud.Plugin.Services;
using Pal.Client.DependencyInjection;
using Pal.Client.Extensions;
using Pal.Client.Floors;
using Pal.Client.Rendering;

namespace Pal.Client.Commands
{
    internal sealed class PalNearCommand : ISubCommand
    {
        private readonly Chat _chat;
        private readonly IClientState _clientState;
        private readonly TerritoryState _territoryState;
        private readonly FloorService _floorService;

        public PalNearCommand(Chat chat, IClientState clientState, TerritoryState territoryState,
            FloorService floorService)
        {
            _chat = chat;
            _clientState = clientState;
            _territoryState = territoryState;
            _floorService = floorService;
        }


        public IReadOnlyDictionary<string, Action<string>> GetHandlers()
            => new Dictionary<string, Action<string>>
            {
                { "near", _ => DebugNearest(_ => true) },
                { "tnear", _ => DebugNearest(m => m.Type == MemoryLocation.EType.Trap) },
                { "hnear", _ => DebugNearest(m => m.Type == MemoryLocation.EType.Hoard) },
            };

        private void DebugNearest(Predicate<PersistentLocation> predicate)
        {
            if (!_territoryState.IsInDeepDungeon())
                return;

            var state = _floorService.GetTerritoryIfReady(_clientState.TerritoryType);
            if (state == null)
                return;

            var playerPosition = _clientState.LocalPlayer?.Position;
            if (playerPosition == null)
                return;
            _chat.Message($"Your position: {playerPosition}");

            var nearbyMarkers = state.Locations
                .Where(m => predicate(m))
                .Where(m => m.RenderElement != null && m.RenderElement.Color != RenderData.ColorInvisible)
                .Select(m => new { m, distance = (playerPosition.Value - m.Position).Length() })
                .OrderBy(m => m.distance)
                .Take(5)
                .ToList();
            foreach (var nearbyMarker in nearbyMarkers)
                _chat.UnformattedMessage(
                    $"{nearbyMarker.distance:F2} - {nearbyMarker.m.Type} {nearbyMarker.m.NetworkId?.ToPartialId(length: 8)} - {nearbyMarker.m.Position}");
        }
    }
}
