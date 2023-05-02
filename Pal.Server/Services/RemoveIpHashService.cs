using Pal.Server.Database;

namespace Pal.Server.Services
{
    internal sealed class RemoveIpHashService : IHostedService, IDisposable
    {
        private readonly ILogger<RemoveIpHashService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _removeAfter = TimeSpan.FromHours(48);
        private Timer? _timer;

        public RemoveIpHashService(ILogger<RemoveIpHashService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }


        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting {ServiceName}", nameof(RemoveIpHashService));
            _timer = new Timer(DoWork, null, TimeSpan.FromSeconds(1), TimeSpan.FromHours(1));
            return Task.CompletedTask;
        }

        private void DoWork(object? state)
        {
            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<PalServerContext>();

            DateTime expiry = DateTime.Now - _removeAfter;
            var accounts = dbContext.Accounts.Where(a => a.IpHash != null && a.CreatedAt < expiry).ToList();
            foreach (var account in accounts)
                account.IpHash = null;

            int saved = dbContext.SaveChanges();
            _logger.LogInformation("Removed IpHash from {Count} accounts", saved);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping {ServiceName}", nameof(RemoveIpHashService));
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
