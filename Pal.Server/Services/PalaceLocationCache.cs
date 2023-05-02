using Palace;
using System.Collections.Concurrent;

namespace Pal.Server.Services
{
    internal sealed class PalaceLocationCache
    {
        private readonly ConcurrentDictionary<ushort, ConcurrentDictionary<Guid, PalaceObject>> _objects = new();

        internal ConcurrentDictionary<Guid, PalaceObject> Add(ushort territoryType, ConcurrentDictionary<Guid, PalaceObject> objects)
        {
            _objects[territoryType] = objects;
            return objects;
        }

        internal bool TryGetValue(ushort territoryType, out ConcurrentDictionary<Guid, PalaceObject>? objects) =>
            _objects.TryGetValue(territoryType, out objects);
    }
}
