using System;
using System.IO;
using Dalamud.Data;
using Dalamud.Extensions.MicrosoftLogging;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
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
using Pal.Client.Net;
using Pal.Client.Rendering;
using Pal.Client.Scheduled;
using Pal.Client.Windows;

namespace Pal.Client
{
    /// <summary>
    /// DI-aware Plugin.
    /// </summary>
    internal sealed class DependencyInjectionContext : IDisposable
    {
        public const string DatabaseFileName = "palace-pal.data.sqlite3";
        public static DalamudLoggerProvider LoggerProvider { get; } = new(typeof(Plugin).Assembly);

        /// <summary>
        /// Initialized as temporary logger, will be overriden once context is ready with a logger that supports scopes.
        /// </summary>
        private ILogger _logger = LoggerProvider.CreateLogger<DependencyInjectionContext>();

        private readonly string _sqliteConnectionString;
        private readonly ServiceCollection _serviceCollection = new();
        private ServiceProvider? _serviceProvider;

        public DependencyInjectionContext(
            DalamudPluginInterface pluginInterface,
            IClientState clientState,
            IGameGui gameGui,
            IChatGui chatGui,
            IObjectTable objectTable,
            IFramework framework,
            ICondition condition,
            ICommandManager commandManager,
            IDataManager dataManager,
            Plugin plugin)
        {
            _logger.LogInformation("Building dalamud service container for {Assembly}",
                typeof(DependencyInjectionContext).Assembly.FullName);

            // set up legacy services
#pragma warning disable CS0612
            JsonFloorState.SetContextProperties(pluginInterface.GetPluginConfigDirectory());
#pragma warning restore CS0612

            // set up logging
            _serviceCollection.AddLogging(builder =>
                builder.AddFilter("Pal", LogLevel.Trace)
                    .AddFilter("Microsoft.EntityFrameworkCore.Database", LogLevel.Warning)
                    .AddFilter("Grpc", LogLevel.Debug)
                    .ClearProviders()
                    .AddDalamudLogger(plugin));

            // dalamud
            _serviceCollection.AddSingleton<IDalamudPlugin>(plugin);
            _serviceCollection.AddSingleton(pluginInterface);
            _serviceCollection.AddSingleton(clientState);
            _serviceCollection.AddSingleton(gameGui);
            _serviceCollection.AddSingleton(chatGui);
            _serviceCollection.AddSingleton<Chat>();
            _serviceCollection.AddSingleton(objectTable);
            _serviceCollection.AddSingleton(framework);
            _serviceCollection.AddSingleton(condition);
            _serviceCollection.AddSingleton(commandManager);
            _serviceCollection.AddSingleton(dataManager);
            _serviceCollection.AddSingleton(new WindowSystem(typeof(DependencyInjectionContext).AssemblyQualifiedName));

            _sqliteConnectionString =
                $"Data Source={Path.Join(pluginInterface.GetPluginConfigDirectory(), DatabaseFileName)}";
        }

        public IServiceProvider BuildServiceContainer()
        {
            _logger.LogInformation("Building async service container for {Assembly}",
                typeof(DependencyInjectionContext).Assembly.FullName);

            // EF core
            _serviceCollection.AddDbContext<PalClientContext>(o => o
                .UseSqlite(_sqliteConnectionString)
                .UseModel(Database.Compiled.PalClientContextModel.Instance));
            _serviceCollection.AddTransient<JsonMigration>();
            _serviceCollection.AddScoped<Cleanup>();

            // plugin-specific
            _serviceCollection.AddScoped<DependencyContextInitializer>();
            _serviceCollection.AddScoped<DebugState>();
            _serviceCollection.AddScoped<GameHooks>();
            _serviceCollection.AddScoped<RemoteApi>();
            _serviceCollection.AddScoped<ConfigurationManager>();
            _serviceCollection.AddScoped<IPalacePalConfiguration>(sp =>
                sp.GetRequiredService<ConfigurationManager>().Load());
            _serviceCollection.AddTransient<RepoVerification>();

            // commands
            _serviceCollection.AddScoped<PalConfigCommand>();
            _serviceCollection.AddScoped<ISubCommand, PalConfigCommand>();
            _serviceCollection.AddScoped<ISubCommand, PalNearCommand>();
            _serviceCollection.AddScoped<ISubCommand, PalStatsCommand>();
            _serviceCollection.AddScoped<ISubCommand, PalTestConnectionCommand>();

            // territory & marker related services
            _serviceCollection.AddScoped<TerritoryState>();
            _serviceCollection.AddScoped<FrameworkService>();
            _serviceCollection.AddScoped<ChatService>();
            _serviceCollection.AddScoped<FloorService>();
            _serviceCollection.AddScoped<ImportService>();

            // windows & related services
            _serviceCollection.AddScoped<AgreementWindow>();
            _serviceCollection.AddScoped<ConfigWindow>();
            _serviceCollection.AddScoped<StatisticsService>();
            _serviceCollection.AddScoped<StatisticsWindow>();

            // rendering
            _serviceCollection.AddScoped<SplatoonRenderer>();
            _serviceCollection.AddScoped<RenderAdapter>();

            // queue handling
            _serviceCollection.AddTransient<IQueueOnFrameworkThread.Handler<QueuedImport>, QueuedImport.Handler>();
            _serviceCollection
                .AddTransient<IQueueOnFrameworkThread.Handler<QueuedUndoImport>, QueuedUndoImport.Handler>();
            _serviceCollection
                .AddTransient<IQueueOnFrameworkThread.Handler<QueuedConfigUpdate>, QueuedConfigUpdate.Handler>();
            _serviceCollection
                .AddTransient<IQueueOnFrameworkThread.Handler<QueuedSyncResponse>, QueuedSyncResponse.Handler>();

            // build
            _serviceProvider = _serviceCollection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });


#if RELEASE
            // You're welcome to remove this code in your fork, but please make sure that:
            // - none of the links accessible within FFXIV open the original repo (e.g. in the plugin installer), and
            // - you host your own server instance
            //
            // This is mainly to avoid this plugin being included in 'mega-repos' that, for whatever reason, decide
            // that collecting all plugins is a good idea (and break half in the process).
            _serviceProvider.GetService<RepoVerification>();
#endif

            // This is not ideal as far as loading the plugin goes, because there's no way to check for errors and
            // tell Dalamud that no, the plugin isn't ready -- so the plugin will count as properly initialized,
            // even if it's not.
            //
            // There's 2-3 seconds of slowdown primarily caused by the sqlite init, but that needs to happen for
            // config stuff.
            _logger = _serviceProvider.GetRequiredService<ILogger<DependencyInjectionContext>>();
            _logger.LogInformation("Service container built");

            return _serviceProvider;
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing DI Context");
            _serviceProvider?.Dispose();

            // ensure we're not keeping the file open longer than the plugin is loaded
            using (SqliteConnection sqliteConnection = new(_sqliteConnectionString))
                SqliteConnection.ClearPool(sqliteConnection);
        }
    }
}
