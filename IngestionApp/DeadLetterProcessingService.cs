using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers.Services.EventQueue;

namespace IngestionApp;

/// <summary>
/// Background service that monitors the dead-letter queue.
/// Logs alerts for failed events that can't be automatically retried.
/// </summary>
internal sealed class DeadLetterProcessingService : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> LogDeadLetterCount =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(1, "DeadLetterCount"),
            "Dead-letter queue has {Count} events requiring manual intervention");

    private static readonly Action<ILogger, int, string, string, Exception?> LogDeadLetterSample =
        LoggerMessage.Define<int, string, string>(
            LogLevel.Warning,
            new EventId(2, "DeadLetterSample"),
            "Dead-letter event {EventId}: {EventType} - {ErrorMessage}");

    private static readonly Action<ILogger, Exception?> LogLoopError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(3, "DeadLetterMonitorError"),
            "DeadLetterProcessingService error");

    private readonly IEventQueueService _eventQueueService;
    private readonly int _checkIntervalMinutes;
    private readonly ILogger<DeadLetterProcessingService> _logger;

    public DeadLetterProcessingService(
        IEventQueueService eventQueueService,
        ILogger<DeadLetterProcessingService> logger,
        int checkIntervalMinutes = 5)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _checkIntervalMinutes = checkIntervalMinutes;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deadLetterEvents = await _eventQueueService.GetDeadLetterEventsAsync(100, stoppingToken)
                    .ConfigureAwait(false);

                if (deadLetterEvents.Count > 0)
                {
                    foreach (var @event in deadLetterEvents.Take(5))
                    {
                        LogDeadLetterSample(
                            _logger,
                            @event.Id,
                            @event.EventType,
                            @event.ErrorMessage ?? string.Empty,
                            null);
                    }

                    // Log the summary last so consumers that poll for it can rely on
                    // the sample lines above already being present.
                    LogDeadLetterCount(_logger, deadLetterEvents.Count, null);
                }

                await Task.Delay(TimeSpan.FromMinutes(_checkIntervalMinutes), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogLoopError(_logger, ex);
            }
        }
    }
}
