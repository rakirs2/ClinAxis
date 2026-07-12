using Microsoft.Extensions.Hosting;
using Scrapers.Services.EventQueue;

namespace IngestionApp;

/// <summary>
/// Core background service that continuously processes events from the queue.
/// Claims events, dispatches to appropriate handlers, and manages retries/dead-letter.
/// </summary>
internal sealed class EventProcessingService : BackgroundService
{
    private readonly IEventQueueService _eventQueueService;
    private readonly string _serviceInstanceId;
    private readonly int _pollIntervalSeconds;
    private readonly int _claimedEventTimeoutMinutes;

    public EventProcessingService(
        IEventQueueService eventQueueService,
        int pollIntervalSeconds = 10,
        int claimedEventTimeoutMinutes = 30)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        _pollIntervalSeconds = pollIntervalSeconds;
        _claimedEventTimeoutMinutes = claimedEventTimeoutMinutes;

        // Unique instance identifier
        _serviceInstanceId = $"{System.Environment.MachineName}-{System.Environment.ProcessId}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Release stuck events
                await _eventQueueService.ReleaseStuckEventsAsync(
                    TimeSpan.FromMinutes(_claimedEventTimeoutMinutes),
                    stoppingToken).ConfigureAwait(false);

                // Try to claim and process one event
                var @event = await _eventQueueService.ClaimNextPendingEventAsync(
                    _serviceInstanceId,
                    stoppingToken).ConfigureAwait(false);

                if (@event == null)
                {
                    // No events to process, sleep briefly
                    await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                // Process the claimed event
                try
                {
                    await DispatchEventAsync(@event, stoppingToken).ConfigureAwait(false);
                    await _eventQueueService.CompleteEventAsync(@event.Id, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await _eventQueueService.FailEventAsync(
                        @event.Id,
                        $"{ex.GetType().Name}: {ex.Message}",
                        stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EventProcessingService error: {ex}");
                await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private static Task DispatchEventAsync(Scrapers.Persistence.Entities.PipelineEventEntity @event, CancellationToken ct)
    {
        // For now, just log that we received the event
        System.Diagnostics.Debug.WriteLine($"Processing event: {@event.EventType} with data: {@event.Data}");

        // Future implementations will dispatch to specific handlers
        switch (@event.EventType)
        {
            case "studies.discovered":
                // Enqueue downstream events for enrichment
                break;

            default:
                // Unknown event type, but don't fail - just complete it
                break;
        }

        return Task.CompletedTask;
    }
}
