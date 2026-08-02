using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers.Services;
using Scrapers.Services.EventQueue;

namespace IngestionApp;

internal sealed class EventProcessingService : BackgroundService
{
    private static readonly Action<ILogger, int, string, Exception?> LogEventFailed =
        LoggerMessage.Define<int, string>(
            LogLevel.Error,
            new EventId(1, "EventFailed"),
            "Event {EventId} ({EventType}) failed");

    private static readonly Action<ILogger, Exception?> LogLoopError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, "ProcessingLoopError"),
            "EventProcessingService error");

    private readonly IEventQueueService _eventQueueService;
    private readonly ClinicalTrialsIngestionService _ingestionService;
    private readonly string _serviceInstanceId;
    private readonly int _pollIntervalSeconds;
    private readonly int _claimedEventTimeoutMinutes;
    private readonly int _ingestTimeoutMinutes;
    private readonly ILogger<EventProcessingService>? _logger;

    public EventProcessingService(
        IEventQueueService eventQueueService,
        ClinicalTrialsIngestionService ingestionService,
        int pollIntervalSeconds = 10,
        int claimedEventTimeoutMinutes = 30,
        int ingestTimeoutMinutes = 60,
        ILogger<EventProcessingService>? logger = null)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        _ingestionService = ingestionService ?? throw new ArgumentNullException(nameof(ingestionService));
        _pollIntervalSeconds = pollIntervalSeconds;
        _claimedEventTimeoutMinutes = claimedEventTimeoutMinutes;
        _ingestTimeoutMinutes = ingestTimeoutMinutes;
        _logger = logger;

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
                    if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                    {
                        LogEventFailed(_logger, @event.Id, @event.EventType, ex);
                    }
                    await _eventQueueService.FailEventAsync(
                        @event.Id,
                        ex.ToString(),
                        stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                {
                    LogLoopError(_logger, ex);
                }
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

        // Bound each ingest call so a stalled DB operation cannot block the queue forever.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(_ingestTimeoutMinutes));
        try
        {
            await _ingestionService.IngestAsync(count, cancellationToken: timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"IngestAsync of {count} studies exceeded the {_ingestTimeoutMinutes} minute timeout; " +
                "the event was failed and will be retried.");
        }
    }
}
