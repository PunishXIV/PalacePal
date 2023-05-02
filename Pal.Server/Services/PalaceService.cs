using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Pal.Common;
using Palace;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Pal.Server.Database;
using static Palace.PalaceService;

namespace Pal.Server.Services
{
    internal sealed class PalaceService : PalaceServiceBase
    {
        private readonly ILogger<PalaceService> _logger;
        private readonly PalServerContext _dbContext;
        private readonly PalaceLocationCache _cache;
        private readonly IEqualityComparer<PalaceObject> _objEqualityComparer = new TypeAndLocationEqualityComparer();

        public PalaceService(ILogger<PalaceService> logger, PalServerContext dbContext, PalaceLocationCache cache)
        {
            _logger = logger;
            _dbContext = dbContext;
            _cache = cache;
        }

        [Authorize]
        public override async Task<DownloadFloorsReply> DownloadFloors(DownloadFloorsRequest request,
            ServerCallContext context)
        {
            try
            {
                ushort territoryType = (ushort)request.TerritoryType;
                if (!typeof(ETerritoryType).IsEnumDefined(territoryType))
                {
                    _logger.LogInformation("Skipping download for unknown territory type {TerritoryType}",
                        territoryType);
                    return new DownloadFloorsReply { Success = false };
                }

                var objects = await GetOrLoadObjects(territoryType, context.CancellationToken);

                var reply = new DownloadFloorsReply { Success = true };
                reply.Objects.AddRange(objects.Values);
                return reply;
            }
            catch (Exception e)
            {
                _logger.LogError("Could not download floors for territory {TerritoryType}: {e}", request.TerritoryType,
                    e);
                return new DownloadFloorsReply { Success = false };
            }
        }

        [Authorize]
        public override async Task<UploadFloorsReply> UploadFloors(UploadFloorsRequest request,
            ServerCallContext context)
        {
            try
            {
                var accountId = context.GetAccountId();
                var territoryType = (ushort)request.TerritoryType;

                if (!typeof(ETerritoryType).IsEnumDefined(territoryType))
                {
                    _logger.LogInformation("Skipping upload for unknown territory type {TerritoryType}", territoryType);
                    return new UploadFloorsReply { Success = false };
                }

                // only happens when the server is being restarted while people are currently doing potd/hoh runs and have downloaded the floor layout prior to the restart
                var objects = await GetOrLoadObjects(territoryType, context.CancellationToken);

                DateTime createdAt = DateTime.Now;
                var newLocations = request.Objects.Where(o => !objects.Values.Contains(o, _objEqualityComparer))
                    .Where(o => o.Type != ObjectType.Unknown && !(o.X == 0 && o.Y == 0 && o.Z == 0))
                    .Where(o => o.Type == ObjectType.Trap || o.Type == ObjectType.Hoard)
                    .Distinct(_objEqualityComparer)
                    .Select(o => new ServerLocation
                    {
                        Id = Guid.NewGuid(),
                        TerritoryType = territoryType,
                        Type = (ServerLocation.EType)o.Type,
                        X = o.X,
                        Y = o.Y,
                        Z = o.Z,
                        AccountId = accountId,
                        CreatedAt = createdAt,
                    })
                    .ToList();
                var reply = new UploadFloorsReply { Success = true };
                if (newLocations.Count > 0)
                {
                    await _dbContext.AddRangeAsync(newLocations, context.CancellationToken);
                    await _dbContext.SaveChangesAsync(context.CancellationToken);

                    foreach (var location in newLocations)
                    {
                        var palaceObj = new PalaceObject
                        {
                            Type = (ObjectType)location.Type,
                            X = location.X,
                            Y = location.Y,
                            Z = location.Z,
                            NetworkId = location.Id.ToString()
                        };
                        objects[location.Id] = palaceObj;
                        reply.Objects.Add(palaceObj);
                    }

                    int trapCount = newLocations.Count(x => x.Type == ServerLocation.EType.Trap);
                    int hoardCount = newLocations.Count(x => x.Type == ServerLocation.EType.Hoard);

                    if (trapCount > 0)
                        _logger.LogInformation("Saved {Count} new trap locations for {TerritoryName} ({TerritoryType})",
                            trapCount, (ETerritoryType)territoryType, territoryType);
                    if (hoardCount > 0)
                        _logger.LogInformation(
                            "Saved {Count} new hoard locations for {TerritoryName} ({TerritoryType})", hoardCount,
                            (ETerritoryType)territoryType, territoryType);
                }
                else
                    _logger.LogInformation(
                        "Saved no objects for {TerritoryName} ({TerritoryType}) - all {Count} already known",
                        (ETerritoryType)territoryType, territoryType, request.Objects.Count);

                return reply;
            }
            catch (Exception e)
            {
                _logger.LogError("Could not save {Count} new objects for territory type {TerritoryType}: {e}",
                    request.Objects.Count, request.TerritoryType, e);
                return new UploadFloorsReply { Success = false };
            }
        }

        [Authorize]
        public override async Task<MarkObjectsSeenReply> MarkObjectsSeen(MarkObjectsSeenRequest request,
            ServerCallContext context)
        {
            try
            {
                ushort territoryType = (ushort)request.TerritoryType;
                if (!typeof(ETerritoryType).IsEnumDefined(territoryType))
                {
                    _logger.LogInformation("Skipping mark objects seen for unknown territory type {TerritoryType}",
                        territoryType);
                    return new MarkObjectsSeenReply { Success = false };
                }

                var account = await _dbContext.Accounts.FindAsync(new object[] { context.GetAccountId() },
                    cancellationToken: context.CancellationToken);
                if (account == null)
                {
                    _logger.LogInformation("Skipping mark objects seen, account {} not found", context.GetAccountId());
                    return new MarkObjectsSeenReply { Success = false };
                }

                var objects = await GetOrLoadObjects(territoryType, context.CancellationToken);

                DateTime firstSeenAt = DateTime.Now;
                var seenLocations = account.SeenLocations;
                var newLocations = request.NetworkIds.Select(x => Guid.Parse(x))
                    .Where(x => objects.ContainsKey(x))
                    .Where(x => !seenLocations.Any(seen => seen.PalaceLocationId == x))
                    .Select(x => new SeenLocation(account, x, firstSeenAt))
                    .ToList();
                if (newLocations.Count > 0)
                {
                    _logger.LogInformation(
                        "Mark {} locations as seen for account {} on territory {TerritoryName} ({TerritoryType})",
                        newLocations.Count, account.Id, (ETerritoryType)territoryType, territoryType);
                    account.SeenLocations.AddRange(newLocations);
                    await _dbContext.SaveChangesAsync(context.CancellationToken);
                }

                return new MarkObjectsSeenReply { Success = true };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not mark objects seen for territory {TerritoryType}: {e}",
                    request.TerritoryType, e);
                return new MarkObjectsSeenReply { Success = false };
            }
        }

        [Authorize(Roles = "statistics:view")]
        public override async Task<StatisticsReply> FetchStatistics(StatisticsRequest request,
            ServerCallContext context)
        {
            try
            {
                var reply = new StatisticsReply { Success = true };
                foreach (ETerritoryType territoryType in typeof(ETerritoryType).GetEnumValues())
                {
                    var objects = await GetOrLoadObjects((ushort)territoryType, context.CancellationToken);
                    reply.FloorStatistics.Add(new FloorStatistics
                    {
                        TerritoryType = (ushort)territoryType,
                        TrapCount = (uint)objects.Values.Count(x => x.Type == ObjectType.Trap),
                        HoardCount = (uint)objects.Values.Count(x => x.Type == ObjectType.Hoard),
                    });
                }

                return reply;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not fetch statistics: {e}", e);
                return new StatisticsReply { Success = false };
            }
        }

        private async Task<ConcurrentDictionary<Guid, PalaceObject>> GetOrLoadObjects(ushort territoryType,
            CancellationToken cancellationToken)
        {
            if (!_cache.TryGetValue(territoryType, out var objects))
                objects = await LoadObjects(territoryType, cancellationToken);

            return objects ?? throw new Exception($"Unable to load objects for territory type {territoryType}");
        }

        private async Task<ConcurrentDictionary<Guid, PalaceObject>> LoadObjects(ushort territoryType,
            CancellationToken cancellationToken)
        {
            var objects = await _dbContext.Locations.Where(o => o.TerritoryType == territoryType)
                .ToDictionaryAsync(o => o.Id,
                    o => new PalaceObject
                    { Type = (ObjectType)o.Type, X = o.X, Y = o.Y, Z = o.Z, NetworkId = o.Id.ToString() },
                    cancellationToken);

            var result = _cache.Add(territoryType, new ConcurrentDictionary<Guid, PalaceObject>(objects));
            return result;
        }

        private sealed class TypeAndLocationEqualityComparer : IEqualityComparer<PalaceObject>
        {
            public bool Equals(PalaceObject? first, PalaceObject? second)
            {
                if (first == null && second == null)
                    return true;
                else if (first == null || second == null)
                    return false;
                else
                    return first.Type == second.Type
                           && PalaceMath.IsNearlySamePosition(new Vector3(first.X, first.Y, first.Z),
                               new Vector3(second.X, second.Y, second.Z));
            }

            public int GetHashCode(PalaceObject obj)
            {
                return HashCode.Combine(obj.Type, PalaceMath.GetHashCode(new Vector3(obj.X, obj.Y, obj.Z)));
            }
        }
    }
}
