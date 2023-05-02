using Microsoft.EntityFrameworkCore;

namespace Pal.Server.Database
{
    [Index("AccountId", "PalaceLocationId", IsUnique = true)]
    public sealed class SeenLocation
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public Account Account { get; set; } = null!;
        public Guid PalaceLocationId { get; set; }
        public ServerLocation PalaceLocation { get; set; } = null!;
        public DateTime FirstSeenAt { get; set; }

        private SeenLocation() { }

        public SeenLocation(Account account, Guid palaceLocationId, DateTime firstSeenAt)
        {
            Account = account;
            PalaceLocationId = palaceLocationId;
            FirstSeenAt = firstSeenAt;
        }
    }
}
