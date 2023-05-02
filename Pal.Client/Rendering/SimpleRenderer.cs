using Dalamud.Interface;
using ImGuiNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using Pal.Client.Floors;

namespace Pal.Client.Rendering
{
    /// <summary>
    /// Simple renderer that only draws basic stuff. 
    /// 
    /// This is based on what SliceIsRight uses, and what PalacePal used before it was
    /// remade into PalacePal (which is the third or fourth iteration on the same idea
    /// I made, just with a clear vision).
    /// </summary>
    internal sealed class SimpleRenderer : IRenderer, IDisposable
    {
        private const int SegmentCount = 20;

        private readonly ClientState _clientState;
        private readonly GameGui _gameGui;
        private readonly IPalacePalConfiguration _configuration;
        private readonly TerritoryState _territoryState;
        private readonly ConcurrentDictionary<ELayer, SimpleLayer> _layers = new();

        public SimpleRenderer(ClientState clientState, GameGui gameGui, IPalacePalConfiguration configuration,
            TerritoryState territoryState)
        {
            _clientState = clientState;
            _gameGui = gameGui;
            _configuration = configuration;
            _territoryState = territoryState;
        }

        public void SetLayer(ELayer layer, IReadOnlyList<IRenderElement> elements)
        {
            _layers[layer] = new SimpleLayer
            {
                TerritoryType = _clientState.TerritoryType,
                Elements = elements.Cast<SimpleElement>().ToList()
            };
        }

        public void ResetLayer(ELayer layer)
        {
            if (_layers.Remove(layer, out var l))
                l.Dispose();
        }

        public IRenderElement CreateElement(MemoryLocation.EType type, Vector3 pos, uint color, bool fill = false)
        {
            var config = MarkerConfig.ForType(type);
            return new SimpleElement
            {
                Type = type,
                Position = pos + new Vector3(0, config.OffsetY, 0),
                Color = color,
                Radius = config.Radius,
                Fill = fill,
            };
        }

        public void DrawDebugItems(uint trapColor, uint hoardColor)
        {
            _layers[ELayer.Test] = new SimpleLayer
            {
                TerritoryType = _clientState.TerritoryType,
                Elements = new List<SimpleElement>
                {
                    (SimpleElement)CreateElement(
                        MemoryLocation.EType.Trap,
                        _clientState.LocalPlayer?.Position ?? default,
                        trapColor),
                    (SimpleElement)CreateElement(
                        MemoryLocation.EType.Hoard,
                        _clientState.LocalPlayer?.Position ?? default,
                        hoardColor)
                },
                ExpiresAt = Environment.TickCount64 + RenderData.TestLayerTimeout
            };
        }

        public void DrawLayers()
        {
            if (_layers.Count == 0)
                return;

            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(Vector2.Zero, ImGuiCond.None, Vector2.Zero);
            ImGui.SetNextWindowSize(ImGuiHelpers.MainViewport.Size);
            if (ImGui.Begin("###PalacePalSimpleRender",
                    ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground |
                    ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.AlwaysUseWindowPadding))
            {
                foreach (var layer in _layers.Values.Where(l => l.IsValid(_clientState)))
                {
                    foreach (var e in layer.Elements)
                        Draw(e);
                }

                foreach (var key in _layers.Where(l => !l.Value.IsValid(_clientState))
                             .Select(l => l.Key)
                             .ToList())
                    ResetLayer(key);

                ImGui.End();
            }

            ImGui.PopStyleVar();
        }

        private void Draw(SimpleElement e)
        {
            if (e.Color == RenderData.ColorInvisible)
                return;

            switch (e.Type)
            {
                case MemoryLocation.EType.Hoard:
                    // ignore distance if this is a found hoard coffer
                    if (_territoryState.PomanderOfIntuition == PomanderState.Active &&
                        _configuration.DeepDungeons.HoardCoffers.OnlyVisibleAfterPomander)
                        break;

                    goto case MemoryLocation.EType.Trap;

                case MemoryLocation.EType.Trap:
                    var playerPos = _clientState.LocalPlayer?.Position;
                    if (playerPos == null)
                        return;

                    if ((playerPos.Value - e.Position).Length() > 65)
                        return;
                    break;
            }

            bool onScreen = false;
            for (int index = 0; index < 2 * SegmentCount; ++index)
            {
                onScreen |= _gameGui.WorldToScreen(new Vector3(
                        e.Position.X + e.Radius * (float)Math.Sin(Math.PI / SegmentCount * index),
                        e.Position.Y,
                        e.Position.Z + e.Radius * (float)Math.Cos(Math.PI / SegmentCount * index)),
                    out Vector2 vector2);

                ImGui.GetWindowDrawList().PathLineTo(vector2);
            }

            if (onScreen)
            {
                if (e.Fill)
                    ImGui.GetWindowDrawList().PathFillConvex(e.Color);
                else
                    ImGui.GetWindowDrawList().PathStroke(e.Color, ImDrawFlags.Closed, 2);
            }
            else
                ImGui.GetWindowDrawList().PathClear();
        }

        public ERenderer GetConfigValue()
            => ERenderer.Simple;

        public void Dispose()
        {
            foreach (var l in _layers.Values)
                l.Dispose();
        }

        public sealed class SimpleLayer : IDisposable
        {
            public required ushort TerritoryType { get; init; }
            public required IReadOnlyList<SimpleElement> Elements { get; init; }
            public long ExpiresAt { get; init; } = long.MaxValue;

            public bool IsValid(ClientState clientState) =>
                TerritoryType == clientState.TerritoryType && ExpiresAt >= Environment.TickCount64;

            public void Dispose()
            {
                foreach (var e in Elements)
                    e.IsValid = false;
            }
        }

        public sealed class SimpleElement : IRenderElement
        {
            public bool IsValid { get; set; } = true;
            public required MemoryLocation.EType Type { get; init; }
            public required Vector3 Position { get; init; }
            public required uint Color { get; set; }
            public required float Radius { get; init; }
            public required bool Fill { get; init; }
        }
    }
}
