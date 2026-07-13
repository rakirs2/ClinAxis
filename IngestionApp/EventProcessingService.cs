using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Scrapers.Services;
using Scrapers.Services.EventQueue;

namespace IngestionApp;

internal sealed class EventProcessingService : BackgroundService
{
    private readonly IEventQueueService _eventQueueService;
    private readonly ClinicalTrialsIngestionService _ingestionService;
    private readonly string _serviceInstanceId;
    private readonly int _pollIntervalSeconds;
    private readonly int _claimedEventTimeoutMinutes;

    public EventProcessingService(
        IEventQueueService eventQueueService,
        ClinicalTrialsIngestionService ingestionService,
        int pollIntervalSeconds = 10,
        int claimedEventTimeoutMinutes = 30)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        _ingestionService = ingestionService ?? throw new ArgumentNullException(nameof(ingestionService));
        _pollIntervalSeconds = pollIntervalSeconds;
        _claimedEventTimeoutMinutes = claimedEventTimeoutMinutes;

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

                // Try to claim and process one event (we only handle studies.discovered)
                var @event = await _eventQueueService.ClaimNextPendingEventAsync(
                    _serviceInstanceId,
                    eventTypes: ["studies.discovered"],
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

    private async Task DispatchEventAsync(Scrapers.Persistence.Entities.PipelineEventEntity @event, CancellationToken ct)
    {
        await HandleStudiesDiscoveredAsync(@event, ct).ConfigureAwait(false);
    }

    private async Task HandleStudiesDiscoveredAsync(Scrapers.Persistence.Entities.PipelineEventEntity @event, CancellationToken ct)
    {
        var count = 50;
        if (!string.IsNullOrWhiteSpace(@event.Data))
        {
            using var doc = JsonDocument.Parse(@event.Data);
            if (doc.RootElement.TryGetProperty("count", out var countProp))
            {
                count = countProp.GetInt32();
            }
        }

        await _ingestionService.IngestAsync(count, cancellationToken: ct).ConfigureAwait(false);
    }
}
