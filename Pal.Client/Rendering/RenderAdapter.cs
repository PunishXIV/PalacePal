using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;
using Pal.Client.Floors;

namespace Pal.Client.Rendering
{
    internal sealed class RenderAdapter : IRenderer, IDisposable
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<RenderAdapter> _logger;
        private readonly IPalacePalConfiguration _configuration;

        private IServiceScope? _renderScope;
        private IRenderer _implementation;

        public RenderAdapter(IServiceScopeFactory serviceScopeFactory, ILogger<RenderAdapter> logger,
            IPalacePalConfiguration configuration)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _configuration = configuration;

            _implementation = Recreate(null);
        }

        public bool RequireRedraw { get; set; }

        private IRenderer Recreate(ERenderer? currentRenderer)
        {
            ERenderer targetRenderer = _configuration.Renderer.SelectedRenderer;
            if (targetRenderer == currentRenderer)
                return _implementation;

            _renderScope?.Dispose();

            _logger.LogInformation("Selected new renderer: {Renderer}", _configuration.Renderer.SelectedRenderer);
            _renderScope = _serviceScopeFactory.CreateScope();
            if (targetRenderer == ERenderer.Splatoon)
                return _renderScope.ServiceProvider.GetRequiredService<SplatoonRenderer>();
            else
                return _renderScope.ServiceProvider.GetRequiredService<SimpleRenderer>();
        }

        public void ConfigUpdated()
        {
            _implementation = Recreate(_implementation.GetConfigValue());
            RequireRedraw = true;
        }

        public void Dispose()
            => _renderScope?.Dispose();

        public void SetLayer(ELayer layer, IReadOnlyList<IRenderElement> elements)
            => _implementation.SetLayer(layer, elements);

        public void ResetLayer(ELayer layer)
            => _implementation.ResetLayer(layer);

        public IRenderElement CreateElement(MemoryLocation.EType type, Vector3 pos, uint color, bool fill = false)
            => _implementation.CreateElement(type, pos, color, fill);

        public ERenderer GetConfigValue()
            => throw new NotImplementedException();

        public void DrawDebugItems(uint trapColor, uint hoardColor)
            => _implementation.DrawDebugItems(trapColor, hoardColor);

        public void DrawLayers()
        {
            if (_implementation is SimpleRenderer sr)
                sr.DrawLayers();
        }
    }
}
