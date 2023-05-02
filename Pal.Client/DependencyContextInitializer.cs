using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Commands;
using Pal.Client.Configuration;
using Pal.Client.Configuration.Legacy;
using Pal.Client.Database;
using Pal.Client.DependencyInjection;
using Pal.Client.Floors;
using Pal.Client.Windows;

namespace Pal.Client
{
    /// <summary>
    /// Takes care of async plugin init - this is mostly everything that requires either the config or the database to
    /// be available.
    /// </summary>
    internal sealed class DependencyContextInitializer
    {
        private readonly ILogger<DependencyContextInitializer> _logger;
        private readonly IServiceProvider _serviceProvider;

        public DependencyContextInitializer(ILogger<DependencyContextInitializer> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            using IDisposable? logScope = _logger.BeginScope("AsyncInit");

            _logger.LogInformation("Starting async init");

            await CreateBackup();
            cancellationToken.ThrowIfCancellationRequested();

            await RunMigrations(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // v1 migration: config migration for import history, json migration for markers
            _serviceProvider.GetRequiredService<ConfigurationManager>().Migrate();
            await _serviceProvider.GetRequiredService<JsonMigration>().MigrateAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await RunCleanup();
            cancellationToken.ThrowIfCancellationRequested();

            await RemoveOldBackups();
            cancellationToken.ThrowIfCancellationRequested();

            // windows that have logic to open on startup
            _serviceProvider.GetRequiredService<AgreementWindow>();

            // initialize components that are mostly self-contained/self-registered
            _serviceProvider.GetRequiredService<GameHooks>();
            _serviceProvider.GetRequiredService<FrameworkService>();
            _serviceProvider.GetRequiredService<ChatService>();

            // eager load any commands to find errors now, not when running them
            _serviceProvider.GetRequiredService<IEnumerable<ISubCommand>>();

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Async init complete");
        }

        private async Task RemoveOldBackups()
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var pluginInterface = scope.ServiceProvider.GetRequiredService<DalamudPluginInterface>();
            var configuration = scope.ServiceProvider.GetRequiredService<IPalacePalConfiguration>();

            var paths = Directory.GetFiles(pluginInterface.GetPluginConfigDirectory(), "backup-*.data.sqlite3",
                new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    MatchCasing = MatchCasing.CaseSensitive,
                    AttributesToSkip = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System,
                    ReturnSpecialDirectories = false,
                });
            if (paths.Length == 0)
                return;

            Regex backupRegex = new Regex(@"backup-([\d\-]{10})\.data\.sqlite3", RegexOptions.Compiled);
            List<(DateTime Date, string Path)> backupFiles = new();
            foreach (string path in paths)
            {
                var match = backupRegex.Match(Path.GetFileName(path));
                if (!match.Success)
                    continue;

                if (DateTime.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out DateTime backupDate))
                {
                    backupFiles.Add((backupDate, path));
                }
            }

            var toDelete = backupFiles.OrderByDescending(x => x.Date)
                .Skip(configuration.Backups.MinimumBackupsToKeep)
                .Where(x => (DateTime.Now.ToUniversalTime() - x.Date).Days > configuration.Backups.DaysToDeleteAfter)
                .Select(x => x.Path);
            foreach (var path in toDelete)
            {
                try
                {
                    File.Delete(path);
                    _logger.LogInformation("Deleted old backup file '{Path}'", path);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Could not delete backup file '{Path}'", path);
                }
            }
        }

        private async Task CreateBackup()
        {
            await using var scope = _serviceProvider.CreateAsyncScope();

            var pluginInterface = scope.ServiceProvider.GetRequiredService<DalamudPluginInterface>();
            string backupPath = Path.Join(pluginInterface.GetPluginConfigDirectory(),
                $"backup-{DateTime.Now.ToUniversalTime():yyyy-MM-dd}.data.sqlite3");
            string sourcePath = Path.Join(pluginInterface.GetPluginConfigDirectory(),
                DependencyInjectionContext.DatabaseFileName);
            if (File.Exists(sourcePath) && !File.Exists(backupPath))
            {
                try
                {
                    if (File.Exists(sourcePath + "-shm") || File.Exists(sourcePath + "-wal"))
                    {
                        _logger.LogInformation("Creating database backup '{Path}' (open db)", backupPath);
                        await using var db = scope.ServiceProvider.GetRequiredService<PalClientContext>();
                        await using SqliteConnection source = new(db.Database.GetConnectionString());
                        await source.OpenAsync();
                        await using SqliteConnection backup = new($"Data Source={backupPath}");
                        source.BackupDatabase(backup);
                        SqliteConnection.ClearPool(backup);
                    }
                    else
                    {
                        _logger.LogInformation("Creating database backup '{Path}' (file copy)", backupPath);
                        File.Copy(sourcePath, backupPath);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not create backup");
                }
            }
            else
                _logger.LogInformation("Database backup in '{Path}' already exists", backupPath);
        }

        private async Task RunMigrations(CancellationToken cancellationToken)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();

            _logger.LogInformation("Loading database & running migrations");
            await using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            // takes 2-3 seconds with initializing connections, loading driver etc.
            await dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Completed database migrations");
        }

        private async Task RunCleanup()
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            await using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();
            var cleanup = scope.ServiceProvider.GetRequiredService<Cleanup>();

            cleanup.Purge(dbContext);

            await dbContext.SaveChangesAsync();
        }
    }
}
