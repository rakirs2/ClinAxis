using System.Data.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers.Services.EventQueue;

namespace DataApi.Services;

internal sealed class EventQueueTelemetryCache : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> LogRefreshFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, "TelemetryRefreshFailed"),
            "Event queue telemetry refresh failed: {Reason}");

    private readonly string _connectionString;
    private readonly ILogger<EventQueueTelemetryCache> _logger;
    private EventQueueTelemetryResponse _current = EventQueueTelemetryResponse.Warming();

    public EventQueueTelemetryCache(string connectionString, ILogger<EventQueueTelemetryCache> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public EventQueueTelemetryResponse Current => Volatile.Read(ref _current);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await RefreshAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RefreshAsync(CancellationToken stoppingToken)
    {
        using var refreshCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        refreshCts.CancelAfter(TimeSpan.FromSeconds(25));

        try
        {
            var eventQueueService = new EventQueueService(_connectionString);
            Task<EventQueueStats> statsTask = eventQueueService.GetStatsAsync(refreshCts.Token);
            Task<List<EventTypeBreakdown>> breakdownTask = eventQueueService.GetEventTypeBreakdownAsync(refreshCts.Token);
            await Task.WhenAll(statsTask, breakdownTask).ConfigureAwait(false);

            var stats = await statsTask.ConfigureAwait(false);
            var byEventType = await breakdownTask.ConfigureAwait(false);
            Volatile.Write(ref _current, new EventQueueTelemetryResponse
            {
                PendingCount = stats.PendingCount,
                ProcessingCount = stats.ProcessingCount,
                CompletedCount = stats.CompletedCount,
                DeadLetterCount = stats.DeadLetterCount,
                FailedCount = stats.FailedCount,
                AverageProcessingTimeMs = stats.AverageProcessingTimeMs,
                FailureRate = stats.FailureRate,
                EstimatedTimeRemainingMs = stats.EstimatedTimeRemainingMs,
                ByEventType = byEventType,
                TelemetryStatus = "healthy",
                TelemetryUpdatedAt = DateTime.UtcNow,
                TelemetryError = null
            });
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            MarkUnavailable("refresh timed out");
        }
        catch (DbException ex)
        {
            MarkUnavailable("database refresh failed");
            LogRefreshFailed(_logger, "database refresh failed", ex);
        }
        catch (InvalidOperationException ex)
        {
            MarkUnavailable("telemetry refresh failed");
            LogRefreshFailed(_logger, "telemetry refresh failed", ex);
        }
    }

    private void MarkUnavailable(string reason)
    {
        EventQueueTelemetryResponse previous = Current;
        Volatile.Write(ref _current, previous with
        {
            TelemetryStatus = previous.TelemetryUpdatedAt.HasValue ? "stale" : "warming",
            TelemetryError = reason
        });
    }
}

internal sealed record EventQueueTelemetryResponse
{
    public int PendingCount { get; init; }
    public int ProcessingCount { get; init; }
    public int CompletedCount { get; init; }
    public int DeadLetterCount { get; init; }
    public int FailedCount { get; init; }
    public double AverageProcessingTimeMs { get; init; }
    public double FailureRate { get; init; }
    public double? EstimatedTimeRemainingMs { get; init; }
    public IReadOnlyList<EventTypeBreakdown> ByEventType { get; init; } = [];
    public string TelemetryStatus { get; init; } = "warming";
    public DateTime? TelemetryUpdatedAt { get; init; }
    public string? TelemetryError { get; init; }

    public static EventQueueTelemetryResponse Warming() => new();
}
