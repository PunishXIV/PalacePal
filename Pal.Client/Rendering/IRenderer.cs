using System.Collections.Generic;
using System.Numerics;
using Pal.Client.Configuration;
using Pal.Client.Floors;

namespace Pal.Client.Rendering
{
    internal interface IRenderer
    {
        ERenderer GetConfigValue();

        void SetLayer(ELayer layer, IReadOnlyList<IRenderElement> elements);

        void ResetLayer(ELayer layer);

        IRenderElement CreateElement(MemoryLocation.EType type, Vector3 pos, uint color, bool fill = false);

        void DrawDebugItems(uint trapColor, uint hoardColor);
    }
}
