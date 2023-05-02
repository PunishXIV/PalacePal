using Microsoft.EntityFrameworkCore;

namespace Pal.Client.Database
{
    internal class PalClientContext : DbContext
    {
        public DbSet<ClientLocation> Locations { get; set; } = null!;
        public DbSet<ImportHistory> Imports { get; set; } = null!;
        public DbSet<RemoteEncounter> RemoteEncounters { get; set; } = null!;

        public PalClientContext(DbContextOptions<PalClientContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClientLocation>()
                .HasMany(o => o.ImportedBy)
                .WithMany(o => o.ImportedLocations)
                .UsingEntity(o => o.ToTable("LocationImports"));
        }
    }
}
