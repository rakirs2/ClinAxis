using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers.Services;
using Scrapers.Services.EventQueue;
using Scrapers.Utilities;

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

    private static readonly Action<ILogger, int, Exception?> LogProgressWriteFailed =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(3, "ProgressWriteFailed"),
            "Failed to record in-flight progress for event {EventId}; continuing");

    private readonly IEventQueueService _eventQueueService;
    private readonly IDataSourceStateService _dataSourceStateService;
    private readonly ClinicalTrialsIngestionService _ingestionService;
    private readonly string _serviceInstanceId;
    private readonly int _pollIntervalSeconds;
    private readonly int _claimedEventTimeoutMinutes;
    private readonly int _ingestTimeoutMinutes;
    private readonly int _backfillIngestTimeoutHours;
    private readonly ILogger<EventProcessingService>? _logger;

    public EventProcessingService(
        IEventQueueService eventQueueService,
        IDataSourceStateService dataSourceStateService,
        ClinicalTrialsIngestionService ingestionService,
        int pollIntervalSeconds = 10,
        int claimedEventTimeoutMinutes = 30,
        int ingestTimeoutMinutes = 60,
        int backfillIngestTimeoutHours = 10,
        ILogger<EventProcessingService>? logger = null)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        _dataSourceStateService = dataSourceStateService ?? throw new ArgumentNullException(nameof(dataSourceStateService));
        _ingestionService = ingestionService ?? throw new ArgumentNullException(nameof(ingestionService));
        _pollIntervalSeconds = pollIntervalSeconds;
        _claimedEventTimeoutMinutes = claimedEventTimeoutMinutes;
        _ingestTimeoutMinutes = ingestTimeoutMinutes;
        _backfillIngestTimeoutHours = backfillIngestTimeoutHours;
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

                // Try to claim and process one event. Discovered events are short and keep
                // the incremental loop fresh; backfill chunks are long-running and only fill
                // idle slots (even-handed scheduling: backfill never starves discovered).
                var @event = await _eventQueueService.ClaimNextPendingEventAsync(
                    _serviceInstanceId,
                    eventTypes: ["studies.discovered"],
                    stoppingToken).ConfigureAwait(false);
                @event ??= await _eventQueueService.ClaimNextPendingEventAsync(
                    _serviceInstanceId,
                    eventTypes: ["studies.backfill"],
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
                    if (@event.EventType == "studies.discovered")
                    {
                        try
                        {
                            await _dataSourceStateService.SetStatusAsync(
                                "ClinicalTrials.gov",
                                "failed",
                                ex.ToString(),
                                stoppingToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception statusException)
                        {
                            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                            {
                                LogEventFailed(_logger, @event.Id, "studies.discovered status update", statusException);
                            }
                        }
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

    /// <summary>
    /// Best-effort live progress write. Telemetry must never fail a long-running ingest:
    /// a transient DB error is logged and swallowed so the batch keeps going.
    /// </summary>
    private async Task ReportProgressAsync(
        Scrapers.Persistence.Entities.PipelineEventEntity @event,
        int processed,
        int total,
        CancellationToken ct)
    {
        try
        {
            await _eventQueueService.UpdateEventProgressAsync(@event.Id, processed, total, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                LogProgressWriteFailed(_logger, @event.Id, ex);
            }
        }
    }

    private async Task DispatchEventAsync(Scrapers.Persistence.Entities.PipelineEventEntity @event, CancellationToken ct)
    {
        switch (@event.EventType)
        {
            case "studies.discovered":
                await HandleStudiesDiscoveredAsync(@event, ct).ConfigureAwait(false);
                break;
            case "studies.backfill":
                await HandleStudiesBackfillAsync(@event, ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unhandled event type '{@event.EventType}'.");
        }
    }

    private async Task HandleStudiesDiscoveredAsync(Scrapers.Persistence.Entities.PipelineEventEntity @event, CancellationToken ct)
    {
        if (!IncrementalDiscoveryEventPayload.TryParse(@event.Data, out IncrementalDiscoveryEventPayload? payload) ||
            payload is null)
        {
            throw new InvalidOperationException(
                $"studies.discovered event {@event.Id} has a malformed payload: {(@event.Data ?? "<null>")}");
        }

        var count = payload.Count;

        // Bound each ingest call so a stalled DB operation cannot block the queue forever.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(_ingestTimeoutMinutes));
        try
        {
            var ingested = await _ingestionService.IngestAsync(
                count,
                payload.LastUpdatedPost,
                payload.LastUpdatedPostTo,
                onBatchProgress: (processed, total) => ReportProgressAsync(@event, processed, total, timeoutCts.Token),
                cancellationToken: timeoutCts.Token).ConfigureAwait(false);

            if (ingested < count)
            {
                throw new InvalidOperationException(
                    $"IngestAsync persisted {ingested} of {count} studies for event {@event.Id}; " +
                    "the source cursor will not advance.");
            }

            if (payload.HasWindow)
            {
                await _dataSourceStateService.UpdateLastSyncAsync(
                    "ClinicalTrials.gov",
                    payload.LastUpdatedPostTo!.Value,
                    null,
                    timeoutCts.Token).ConfigureAwait(false);
            }
            else
            {
                // Legacy count-only events were created after the old loop had
                // already advanced the cursor, so clear the failure state without
                // moving that cursor again.
                await _dataSourceStateService.SetStatusAsync(
                    "ClinicalTrials.gov",
                    "idle",
                    ct: timeoutCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"IngestAsync of {count} studies exceeded the {_ingestTimeoutMinutes} minute timeout; " +
                "the event was failed and will be retried.");
        }
    }

    private async Task HandleStudiesBackfillAsync(Scrapers.Persistence.Entities.PipelineEventEntity @event, CancellationToken ct)
    {
        if (!BackfillEventPayload.TryParse(@event.Data, out BackfillEventPayload? payload) || payload is null)
        {
            throw new InvalidOperationException(
                $"studies.backfill event {@event.Id} has a malformed payload: {(@event.Data ?? "<null>")}");
        }

        // Chunks run for hours; the ingest timeout must comfortably exceed the chunk duration
        // so a slow DB cannot fail a healthy chunk, while still bounding a genuinely stuck one.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromHours(_backfillIngestTimeoutHours));
        try
        {
            await _ingestionService.IngestAsync(
                payload.Count,
                lastUpdatedPost: payload.DateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                lastUpdatedPostTo: payload.DateTo.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                lastSeenInSweepUtc: payload.SweepStartedUtc,
                onBatchProgress: (processed, total) => ReportProgressAsync(@event, processed, total, timeoutCts.Token),
                cancellationToken: timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Backfill chunk {payload.ChunkIndex?.ToString(CultureInfo.InvariantCulture) ?? "?"} ({payload.DateFrom:yyyy-MM-dd}..{payload.DateTo:yyyy-MM-dd}) " +
                $"exceeded the {_backfillIngestTimeoutHours} hour timeout; the event was failed and will be retried.");
        }
    }
}
