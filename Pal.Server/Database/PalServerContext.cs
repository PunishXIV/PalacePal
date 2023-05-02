using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Pal.Server.Database
{
    public class PalServerContext : DbContext
    {
        public DbSet<Account> Accounts { get; set; } = null!;
        public DbSet<ServerLocation> Locations { get; set; } = null!;
        public DbSet<GlobalSetting> GlobalSettings { get; set; } = null!;

        public PalServerContext(DbContextOptions<PalServerContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>()
                .Property(a => a.Roles)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                    new ValueComparer<List<string>>(
                        (c1, c2) => (c1 ?? new()).SequenceEqual(c2 ?? new()),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()));
        }
    }
}
