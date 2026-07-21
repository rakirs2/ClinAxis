using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.Services.EventQueue;

/// <summary>
/// Manages the event queue with claim-process-complete pattern.
/// Implements exponential backoff retries: 30s, 2m, 10m, then dead-letter.
/// </summary>
public sealed class EventQueueService : IEventQueueService
{
    private readonly string _connectionString;
    private const int MaxRetries = 3;
    private const int ClaimedEventTimeoutMinutes = 30;
    private static readonly SemaphoreSlim ClaimLock = new(1, 1);

    public EventQueueService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task EnqueueAsync(string eventType, string? data = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type cannot be null or empty", nameof(eventType));

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var @event = new PipelineEventEntity
        {
            EventType = eventType,
            Data = data,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.PipelineEvents.Add(@event);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<PipelineEventEntity?> ClaimNextPendingEventAsync(string claimedBy, string[]? eventTypes = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(claimedBy))
            throw new ArgumentException("Service identifier cannot be null or empty", nameof(claimedBy));

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        // First, release stuck claimed events (claimed > 30 minutes ago)
        await ReleaseStuckEventsAsync(TimeSpan.FromMinutes(ClaimedEventTimeoutMinutes), ct).ConfigureAwait(false);

        // Claim the oldest pending event under a lock to prevent concurrent claim races
        await ClaimLock.WaitAsync(ct).ConfigureAwait(false);
        PipelineEventEntity? @event;
        try
        {
            var claimQuery = context.PipelineEvents.Where(e => e.Status == "pending");
            if (eventTypes is { Length: > 0 })
            {
                claimQuery = claimQuery.Where(e => eventTypes.Contains(e.EventType));
            }
            @event = await claimQuery
                .OrderBy(e => e.CreatedAt)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (@event != null)
            {
                @event.Status = "processing";
                @event.ClaimedBy = claimedBy;
                @event.ClaimedAt = DateTime.UtcNow;
                @event.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            ClaimLock.Release();
        }

        return @event;
    }

    public async Task CompleteEventAsync(int eventId, CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var @event = await context.PipelineEvents.FindAsync(new object[] { eventId }, cancellationToken: ct)
            .ConfigureAwait(false);

        if (@event == null)
            throw new InvalidOperationException($"Event {eventId} not found");

        @event.Status = "completed";
        @event.CompletedAt = DateTime.UtcNow;
        @event.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task ReleaseEventAsync(int eventId, CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var @event = await context.PipelineEvents.FindAsync(new object[] { eventId }, cancellationToken: ct)
            .ConfigureAwait(false);

        if (@event == null)
            throw new InvalidOperationException($"Event {eventId} not found");

        @event.Status = "pending";
        @event.ClaimedBy = null;
        @event.ClaimedAt = null;
        @event.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task FailEventAsync(int eventId, string errorMessage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be null or empty", nameof(errorMessage));

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var @event = await context.PipelineEvents.FindAsync(new object[] { eventId }, cancellationToken: ct)
            .ConfigureAwait(false);

        if (@event == null)
            throw new InvalidOperationException($"Event {eventId} not found");

        @event.RetryCount++;
        @event.ErrorMessage = errorMessage;
        @event.LastErrorAt = DateTime.UtcNow;
        @event.UpdatedAt = DateTime.UtcNow;

        if (@event.RetryCount >= MaxRetries)
        {
            // Move to dead-letter queue
            @event.Status = "dead-letter";
            @event.ClaimedBy = null;
            @event.ClaimedAt = null;
        }
        else
        {
            // Reset to pending for retry with exponential backoff
            @event.Status = "pending";
            @event.ClaimedBy = null;
            @event.ClaimedAt = null;
        }

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<PipelineEventEntity>> GetDeadLetterEventsAsync(int limit = 100, CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        return await context.PipelineEvents
            .Where(e => e.Status == "dead-letter")
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<(List<PipelineEventEntity> Events, int TotalCount)> GetDeadLetterEventsPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var query = context.PipelineEvents
            .Where(e => e.Status == "dead-letter");

        var total = await query.CountAsync(ct).ConfigureAwait(false);

        var events = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (events, total);
    }

    public async Task<EventQueueStats> GetStatsAsync(CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var pending = await context.PipelineEvents.CountAsync(e => e.Status == "pending", cancellationToken: ct)
            .ConfigureAwait(false);
        var processing = await context.PipelineEvents.CountAsync(e => e.Status == "processing", cancellationToken: ct)
            .ConfigureAwait(false);
        var completed = await context.PipelineEvents.CountAsync(e => e.Status == "completed", cancellationToken: ct)
            .ConfigureAwait(false);
        var deadLetter = await context.PipelineEvents.CountAsync(e => e.Status == "dead-letter", cancellationToken: ct)
            .ConfigureAwait(false);
        var failed = await context.PipelineEvents.CountAsync(e => e.Status == "failed", cancellationToken: ct)
            .ConfigureAwait(false);

        var totalProcessed = completed + deadLetter + failed;
        var totalEvents = pending + processing + completed + deadLetter + failed;

        var avgProcessingTime = 0.0;
        if (completed > 0)
        {
            var completedEvents = await context.PipelineEvents
                .Where(e => e.Status == "completed" && e.CompletedAt.HasValue && e.ClaimedAt.HasValue)
                .AsNoTracking()
                .Select(e => new { Claimed = e.ClaimedAt!.Value, Completed = e.CompletedAt!.Value })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (completedEvents.Count > 0)
            {
                avgProcessingTime = completedEvents
                    .Select(e => (e.Completed - e.Claimed).TotalMilliseconds)
                    .Average();
            }
        }

        var failureRate = totalProcessed > 0 ? (double)failed / totalProcessed : 0.0;

        double? estimatedTimeRemainingMs = null;
        if (pending > 0 && avgProcessingTime > 0)
        {
            var effectiveWorkers = Math.Max(processing, 1);
            estimatedTimeRemainingMs = pending * avgProcessingTime / effectiveWorkers;
        }

        return new EventQueueStats
        {
            PendingCount = pending,
            ProcessingCount = processing,
            CompletedCount = completed,
            DeadLetterCount = deadLetter,
            FailedCount = failed,
            AverageProcessingTimeMs = avgProcessingTime,
            FailureRate = failureRate,
            EstimatedTimeRemainingMs = estimatedTimeRemainingMs
        };
    }

    public async Task<List<EventTypeBreakdown>> GetEventTypeBreakdownAsync(CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var groups = await context.PipelineEvents
            .GroupBy(e => e.EventType)
            .Select(g => new
            {
                EventType = g.Key,
                Pending = g.Count(e => e.Status == "pending"),
                Processing = g.Count(e => e.Status == "processing"),
                Completed = g.Count(e => e.Status == "completed"),
                Failed = g.Count(e => e.Status == "failed"),
                DeadLetter = g.Count(e => e.Status == "dead-letter"),
                AvgProcessingMs = g.Where(e => e.Status == "completed" && e.CompletedAt.HasValue && e.ClaimedAt.HasValue)
                    .Average(e => (double?)(e.CompletedAt!.Value - e.ClaimedAt!.Value).TotalMilliseconds) ?? 0.0
            })
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var allDurations = await context.PipelineEvents
            .Where(e => e.Status == "completed" && e.CompletedAt.HasValue && e.ClaimedAt.HasValue)
            .Select(e => new { e.EventType, Ms = (e.CompletedAt!.Value - e.ClaimedAt!.Value).TotalMilliseconds })
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var durationsByType = allDurations
            .GroupBy(d => d.EventType)
            .ToDictionary(g => g.Key, g => g.Select(d => d.Ms).ToList());

        return groups.Select(r =>
        {
            List<double>? durations = null;
            durationsByType.TryGetValue(r.EventType, out durations);

            return new EventTypeBreakdown
            {
                EventType = r.EventType,
                Pending = r.Pending,
                Processing = r.Processing,
                Completed = r.Completed,
                Failed = r.Failed,
                DeadLetter = r.DeadLetter,
                AverageProcessingTimeMs = r.AvgProcessingMs,
                Percentiles = durations is { Count: > 0 } ? DurationPercentileCalculator.ComputePercentiles(durations) : null
            };
        }).ToList();
    }

    public async Task<List<DurationHistoryPoint>> GetDurationHistoryAsync(
        string? eventType = null, string period = "24h", int bucketMinutes = 60, CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var since = period switch
        {
            "1h" => DateTime.UtcNow.AddHours(-1),
            "6h" => DateTime.UtcNow.AddHours(-6),
            "24h" => DateTime.UtcNow.AddHours(-24),
            "7d" => DateTime.UtcNow.AddDays(-7),
            _ => DateTime.UtcNow.AddHours(-24)
        };

        var query = context.PipelineEvents
            .Where(e => e.Status == "completed" && e.CompletedAt.HasValue && e.ClaimedAt.HasValue
                        && e.CompletedAt >= since);

        if (!string.IsNullOrEmpty(eventType))
            query = query.Where(e => e.EventType == eventType);

        var durations = await query
            .Select(e => new
            {
                e.EventType,
                e.CompletedAt,
                Ms = (e.CompletedAt!.Value - e.ClaimedAt!.Value).TotalMilliseconds
            })
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var bucketed = durations
            .GroupBy(d =>
            {
                var totalMinutes = (int)(d.CompletedAt!.Value.ToUniversalTime() - DateTime.UnixEpoch).TotalMinutes;
                var bucketStart = (totalMinutes / bucketMinutes) * bucketMinutes;
                return DateTime.UnixEpoch.AddMinutes(bucketStart);
            })
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var msValues = g.Select(d => d.Ms).ToList();
                var sorted = msValues.OrderBy(m => m).ToList();
                    return new DurationHistoryPoint
                {
                    Bucket = g.Key,
                    Count = sorted.Count,
                    AverageMs = sorted.Average(),
                    P50Ms = DurationPercentileCalculator.Percentile(sorted, 50),
                    P95Ms = DurationPercentileCalculator.Percentile(sorted, 95),
                    P99Ms = DurationPercentileCalculator.Percentile(sorted, 99)
                };
            })
            .ToList();

        return bucketed;
    }

    public async Task RetryDeadLetterEventAsync(int eventId, CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var @event = await context.PipelineEvents.FindAsync(new object[] { eventId }, cancellationToken: ct)
            .ConfigureAwait(false);

        if (@event == null)
            throw new InvalidOperationException($"Event {eventId} not found");

        if (@event.Status != "dead-letter")
            throw new InvalidOperationException($"Event {eventId} is not in dead-letter queue");

        @event.Status = "pending";
        @event.RetryCount = 0;
        @event.ErrorMessage = null;
        @event.ClaimedBy = null;
        @event.ClaimedAt = null;
        @event.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task IgnoreDeadLetterEventAsync(int eventId, CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var @event = await context.PipelineEvents.FindAsync(new object[] { eventId }, cancellationToken: ct)
            .ConfigureAwait(false);

        if (@event == null)
            throw new InvalidOperationException($"Event {eventId} not found");

        if (@event.Status != "dead-letter")
            throw new InvalidOperationException($"Event {eventId} is not in dead-letter queue");

        @event.Status = "completed";
        @event.CompletedAt = DateTime.UtcNow;
        @event.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> RetryEventAsync(int eventId, CancellationToken ct = default)
    {
        try
        {
            await RetryDeadLetterEventAsync(eventId, ct).ConfigureAwait(false);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<bool> IgnoreEventAsync(int eventId, CancellationToken ct = default)
    {
        try
        {
            await IgnoreDeadLetterEventAsync(eventId, ct).ConfigureAwait(false);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public async Task ReleaseStuckEventsAsync(TimeSpan claimTimeout, CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var threshold = DateTime.UtcNow - claimTimeout;

        var stuckEvents = await context.PipelineEvents
            .Where(e => e.Status == "processing" && e.ClaimedAt.HasValue && e.ClaimedAt < threshold)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var @event in stuckEvents)
        {
            @event.Status = "pending";
            @event.ClaimedBy = null;
            @event.ClaimedAt = null;
            @event.UpdatedAt = DateTime.UtcNow;
        }

        if (stuckEvents.Count > 0)
        {
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
