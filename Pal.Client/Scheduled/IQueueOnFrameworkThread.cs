using System.Reflection.Metadata;
using Dalamud.Logging;
using Microsoft.Extensions.Logging;

namespace Pal.Client.Scheduled
{
    internal interface IQueueOnFrameworkThread
    {
        internal interface IHandler
        {
            void RunIfCompatible(IQueueOnFrameworkThread queued, ref bool recreateLayout);
        }

        internal abstract class Handler<T> : IHandler
            where T : IQueueOnFrameworkThread
        {
            protected readonly ILogger<Handler<T>> _logger;

            protected Handler(ILogger<Handler<T>> logger)
            {
                _logger = logger;
            }

            protected abstract void Run(T queued, ref bool recreateLayout);

            public void RunIfCompatible(IQueueOnFrameworkThread queued, ref bool recreateLayout)
            {
                if (queued is T t)
                {
                    _logger.LogDebug("Handling {QueuedType}", queued.GetType());
                    Run(t, ref recreateLayout);
                }
                else
                {
                    _logger.LogError("Could not use queue handler {QueuedType}", queued.GetType());
                }
            }
        }
    }
}
