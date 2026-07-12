using Microsoft.Extensions.Hosting;
using Scrapers.Services.EventQueue;

namespace IngestionApp;

/// <summary>
/// Background service that monitors the dead-letter queue.
/// Logs alerts for failed events that can't be automatically retried.
/// </summary>
public sealed class DeadLetterProcessingService : BackgroundService
{
    private readonly IEventQueueService _eventQueueService;
    private readonly int _checkIntervalMinutes;

    public DeadLetterProcessingService(
        IEventQueueService eventQueueService,
        int checkIntervalMinutes = 5)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
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
                    System.Diagnostics.Debug.WriteLine(
                        $"⚠️ Dead-letter queue has {deadLetterEvents.Count} events requiring manual intervention");

                    foreach (var @event in deadLetterEvents.Take(5))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"  - Event {@event.Id}: {@event.EventType} - {@event.ErrorMessage}");
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(_checkIntervalMinutes), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeadLetterProcessingService error: {ex}");
            }
        }
    }
}
