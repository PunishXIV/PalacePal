using Palace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Pal.Client.Database;
using Pal.Client.Floors;

namespace Pal.Client.Net
{
    internal partial class RemoteApi
    {
        public async Task<(bool, List<PersistentLocation>)> DownloadRemoteMarkers(ushort territoryId, CancellationToken cancellationToken = default)
        {
            if (!await Connect(cancellationToken))
                return (false, new());

            var palaceClient = new PalaceService.PalaceServiceClient(_channel);
            var downloadReply = await palaceClient.DownloadFloorsAsync(new DownloadFloorsRequest { TerritoryType = territoryId }, headers: AuthorizedHeaders(), cancellationToken: cancellationToken);
            return (downloadReply.Success, downloadReply.Objects.Select(CreateLocationFromNetworkObject).ToList());
        }

        public async Task<(bool, List<PersistentLocation>)> UploadLocations(ushort territoryType, IReadOnlyList<PersistentLocation> locations, CancellationToken cancellationToken = default)
        {
            if (locations.Count == 0)
                return (true, new());

            if (!await Connect(cancellationToken))
                return (false, new());

            var palaceClient = new PalaceService.PalaceServiceClient(_channel);
            var uploadRequest = new UploadFloorsRequest
            {
                TerritoryType = territoryType,
            };
            uploadRequest.Objects.AddRange(locations.Select(m => new PalaceObject
            {
                Type = m.Type.ToObjectType(),
                X = m.Position.X,
                Y = m.Position.Y,
                Z = m.Position.Z
            }));
            var uploadReply = await palaceClient.UploadFloorsAsync(uploadRequest, headers: AuthorizedHeaders(), cancellationToken: cancellationToken);
            return (uploadReply.Success, uploadReply.Objects.Select(CreateLocationFromNetworkObject).ToList());
        }

        public async Task<bool> MarkAsSeen(ushort territoryType, IReadOnlyList<PersistentLocation> locations, CancellationToken cancellationToken = default)
        {
            if (locations.Count == 0)
                return true;

            if (!await Connect(cancellationToken))
                return false;

            var palaceClient = new PalaceService.PalaceServiceClient(_channel);
            var seenRequest = new MarkObjectsSeenRequest { TerritoryType = territoryType };
            foreach (var marker in locations)
                seenRequest.NetworkIds.Add(marker.NetworkId.ToString());

            var seenReply = await palaceClient.MarkObjectsSeenAsync(seenRequest, headers: AuthorizedHeaders(), deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: cancellationToken);
            return seenReply.Success;
        }

        private PersistentLocation CreateLocationFromNetworkObject(PalaceObject obj)
        {
            return new PersistentLocation
            {
                Type = obj.Type.ToMemoryType(),
                Position = new Vector3(obj.X, obj.Y, obj.Z),
                NetworkId = Guid.Parse(obj.NetworkId),
                Source = ClientLocation.ESource.Download,
            };
        }

        public async Task<(bool, List<FloorStatistics>)> FetchStatistics(CancellationToken cancellationToken = default)
        {
            if (!await Connect(cancellationToken))
                return new(false, new List<FloorStatistics>());

            var palaceClient = new PalaceService.PalaceServiceClient(_channel);
            var statisticsReply = await palaceClient.FetchStatisticsAsync(new StatisticsRequest(), headers: AuthorizedHeaders(), deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: cancellationToken);
            return (statisticsReply.Success, statisticsReply.FloorStatistics.ToList());
        }
    }
}
