using Dalamud.Plugin;
using ECommons;
using ECommons.Reflection;
using ECommons.Schedulers;
using ECommons.SplatoonAPI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Dalamud.Game.ClientState;
using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using Pal.Client.Floors;

namespace Pal.Client.Rendering
{
    internal sealed class SplatoonRenderer : IRenderer, IDisposable
    {
        private const long OnTerritoryChange = -2;

        private readonly ILogger<SplatoonRenderer> _logger;
        private readonly DebugState _debugState;
        private readonly ClientState _clientState;
        private readonly Chat _chat;

        public SplatoonRenderer(
            ILogger<SplatoonRenderer> logger,
            DalamudPluginInterface pluginInterface,
            IDalamudPlugin dalamudPlugin,
            DebugState debugState,
            ClientState clientState,
            Chat chat)
        {
            _logger = logger;
            _debugState = debugState;
            _clientState = clientState;
            _chat = chat;

            _logger.LogInformation("Initializing splatoon");
            ECommonsMain.Init(pluginInterface, dalamudPlugin, ECommons.Module.SplatoonAPI);
        }

        private bool IsDisposed { get; set; }

        public void SetLayer(ELayer layer, IReadOnlyList<IRenderElement> elements)
        {
            // we need to delay this, as the current framework update could be before splatoon's, in which case it would immediately delete the layout
            _ = new TickScheduler(delegate
            {
                try
                {
                    Splatoon.AddDynamicElements(ToLayerName(layer),
                        elements.Cast<SplatoonElement>().Select(x => x.Delegate).ToArray(),
                        new[] { Environment.TickCount64 + 60 * 60 * 1000, OnTerritoryChange });
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not create splatoon layer {Layer} with {Count} elements", layer,
                        elements.Count);
                    _debugState.SetFromException(e);
                }
            });
        }

        public void ResetLayer(ELayer layer)
        {
            try
            {
                Splatoon.RemoveDynamicElements(ToLayerName(layer));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not reset splatoon layer {Layer}", layer);
            }
        }

        private string ToLayerName(ELayer layer)
            => $"PalacePal.{layer}";

        public IRenderElement CreateElement(MemoryLocation.EType type, Vector3 pos, uint color, bool fill = false)
        {
            MarkerConfig config = MarkerConfig.ForType(type);
            Element element = new Element(ElementType.CircleAtFixedCoordinates)
            {
                refX = pos.X,
                refY = pos.Z, // z and y are swapped
                refZ = pos.Y,
                offX = 0,
                offY = 0,
                offZ = config.OffsetY,
                Filled = fill,
                radius = config.Radius,
                FillStep = 1,
                color = color,
                thicc = 2,
            };
            return new SplatoonElement(this, element);
        }

        public void DrawDebugItems(uint trapColor, uint hoardColor)
        {
            try
            {
                Vector3? pos = _clientState.LocalPlayer?.Position;
                if (pos != null)
                {
                    ResetLayer(ELayer.Test);

                    var elements = new List<IRenderElement>
                    {
                        CreateElement(MemoryLocation.EType.Trap, pos.Value, trapColor),
                        CreateElement(MemoryLocation.EType.Hoard, pos.Value, hoardColor),
                    };

                    if (!Splatoon.AddDynamicElements(ToLayerName(ELayer.Test),
                            elements.Cast<SplatoonElement>().Select(x => x.Delegate).ToArray(),
                            new[] { Environment.TickCount64 + RenderData.TestLayerTimeout }))
                    {
                        _chat.Message("Could not draw markers :(");
                    }
                }
            }
            catch (Exception)
            {
                try
                {
                    var pluginManager = DalamudReflector.GetPluginManager();
                    IList installedPlugins =
                        pluginManager.GetType().GetProperty("InstalledPlugins")?.GetValue(pluginManager) as IList ??
                        new List<object>();

                    foreach (var t in installedPlugins)
                    {
                        AssemblyName? assemblyName =
                            (AssemblyName?)t.GetType().GetProperty("AssemblyName")?.GetValue(t);
                        string? pluginName = (string?)t.GetType().GetProperty("Name")?.GetValue(t);
                        if (assemblyName?.Name == "Splatoon" && pluginName != "Splatoon")
                        {
                            _chat.Error(
                                $"Splatoon is installed under the plugin name '{pluginName}', which is incompatible with the Splatoon API.");
                            _chat.Message(
                                "You need to install Splatoon from the official repository at https://github.com/NightmareXIV/MyDalamudPlugins.");
                            return;
                        }
                    }
                }
                catch (Exception)
                {
                    // not relevant
                }

                _chat.Error("Could not draw markers, is Splatoon installed and enabled?");
            }
        }

        public ERenderer GetConfigValue()
            => ERenderer.Splatoon;

        public void Dispose()
        {
            _logger.LogInformation("Disposing splatoon");

            IsDisposed = true;

            ResetLayer(ELayer.TrapHoard);
            ResetLayer(ELayer.RegularCoffers);
            ResetLayer(ELayer.Test);

            ECommonsMain.Dispose();
        }

        private sealed class SplatoonElement : IRenderElement
        {
            private readonly SplatoonRenderer _renderer;

            public SplatoonElement(SplatoonRenderer renderer, Element element)
            {
                _renderer = renderer;
                Delegate = element;
            }

            public Element Delegate { get; }

            public bool IsValid => !_renderer.IsDisposed && Delegate.IsValid();

            public uint Color
            {
                get => Delegate.color;
                set => Delegate.color = value;
            }
        }
    }
}
