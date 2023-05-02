using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;
using Pal.Client.Floors;
using static Pal.Client.Rendering.SplatoonRenderer;

namespace Pal.Client.Rendering
{
    internal sealed class RenderAdapter : IDisposable
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<RenderAdapter> _logger;
        private readonly IPalacePalConfiguration _configuration;

        private IServiceScope? _renderScope;
        internal SplatoonRenderer _implementation;

        public RenderAdapter(IServiceScopeFactory serviceScopeFactory, ILogger<RenderAdapter> logger,
            IPalacePalConfiguration configuration)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _configuration = configuration;

            _implementation = Recreate(true);
        }

        public bool RequireRedraw { get; set; }

        private SplatoonRenderer Recreate(bool recreate)
        {
            if (!recreate)
                return _implementation;

            _renderScope?.Dispose();

            _logger.LogInformation("Selected new renderer: Splatoon");
            _renderScope = _serviceScopeFactory.CreateScope();
            return _renderScope.ServiceProvider.GetRequiredService<SplatoonRenderer>();
        }

        public void ConfigUpdated()
        {
            RequireRedraw = true;
        }

        public void Dispose()
            => _renderScope?.Dispose();

        public void SetLayer(ELayer layer, IReadOnlyList<SplatoonElement> elements)
            => _implementation.SetLayer(layer, elements);

        public void ResetLayer(ELayer layer)
            => _implementation.ResetLayer(layer);

        public SplatoonElement CreateElement(MemoryLocation.EType type, Vector3 pos, uint color, bool fill = false)
            => _implementation.CreateElement(type, pos, color, fill);

        public void DrawDebugItems(uint trapColor, uint hoardColor)
            => _implementation.DrawDebugItems(trapColor, hoardColor);

        public void DrawLayers()
        {
            
        }

        public void UpdateExitElement()
        {
            _implementation.UpdateExitElement();
        }
    }
}
