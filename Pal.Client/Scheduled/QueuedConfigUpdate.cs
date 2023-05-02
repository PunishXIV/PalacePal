using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using Pal.Client.Floors;
using Pal.Client.Rendering;

namespace Pal.Client.Scheduled
{
    internal sealed class QueuedConfigUpdate : IQueueOnFrameworkThread
    {
        internal sealed class Handler : IQueueOnFrameworkThread.Handler<QueuedConfigUpdate>
        {
            private readonly RenderAdapter _renderAdapter;

            public Handler(
                ILogger<Handler> logger,
                RenderAdapter renderAdapter)
                : base(logger)
            {
                _renderAdapter = renderAdapter;
            }

            protected override void Run(QueuedConfigUpdate queued, ref bool recreateLayout)
            {
                _renderAdapter.ConfigUpdated();
            }
        }
    }
}
