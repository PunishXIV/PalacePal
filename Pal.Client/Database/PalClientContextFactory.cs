#if EF
using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pal.Client.Database
{
    internal sealed class PalClientContextFactory : IDesignTimeDbContextFactory<PalClientContext>
    {
        public PalClientContext CreateDbContext(string[] args)
        {
            var optionsBuilder =
                new DbContextOptionsBuilder<PalClientContext>().UseSqlite(
                    $"Data Source={Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "pluginConfigs", "Palace Pal", "palace-pal.data.sqlite3")}");
            return new PalClientContext(optionsBuilder.Options);
        }
    }
}
#endif
