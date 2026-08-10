using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Utilities;

namespace Scrapers.Services.EventQueue;

/// <summary>
/// Manages the event queue with claim-process-complete pattern.
/// Implements exponential backoff retries: 30s, 2m, 10m, then dead-letter.
/// </summary>
public sealed class EventQueueService : IEventQueueService
{
    private readonly string _connectionString;
    private const int MaxRetries = 4;
    private const int ClaimedEventTimeoutMinutes = 30;
    private const int DefaultBackfillClaimTimeoutHours = 12;
    private const int DurationSampleLimit = 10_000;
    private static readonly SemaphoreSlim ClaimLock = new(1, 1);

    private readonly TimeSpan _backfillClaimTimeout;

    public EventQueueService(string connectionString, int backfillClaimTimeoutHours = DefaultBackfillClaimTimeoutHours)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        if (backfillClaimTimeoutHours <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(backfillClaimTimeoutHours), "backfillClaimTimeoutHours must be greater than 0.");
        }
        _backfillClaimTimeout = TimeSpan.FromHours(backfillClaimTimeoutHours);
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
            var now = DateTime.UtcNow;
            var firstRetryEligibleAt = now - EventRetryBackoff.GetDelay(1);
            var secondRetryEligibleAt = now - EventRetryBackoff.GetDelay(2);
            var thirdRetryEligibleAt = now - EventRetryBackoff.GetDelay(3);
            var claimQuery = context.PipelineEvents
                .Where(e => e.Status == "pending")
                .Where(e => e.RetryCount == 0 ||
                            (e.RetryCount == 1 &&
                             (!e.LastErrorAt.HasValue || e.LastErrorAt <= firstRetryEligibleAt)) ||
                            (e.RetryCount == 2 &&
                             (!e.LastErrorAt.HasValue || e.LastErrorAt <= secondRetryEligibleAt)) ||
                            (e.RetryCount == 3 &&
                             (!e.LastErrorAt.HasValue || e.LastErrorAt <= thirdRetryEligibleAt)));
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

    public async Task<bool> HasActiveEventAsync(string eventType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Event type cannot be null or empty", nameof(eventType));
        }

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        return await context.PipelineEvents
            .AnyAsync(e => e.EventType == eventType &&
                          (e.Status == "pending" || e.Status == "processing"), ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasUnresolvedDiscoveryEventAsync(DateTime? lastUpdatedPost, CancellationToken ct = default)
    {
        var unresolvedEvents = await GetUnresolvedEventDataAsync("studies.discovered", ct).ConfigureAwait(false);
        foreach (var unresolvedEvent in unresolvedEvents)
        {
            if (unresolvedEvent.Status is "pending" or "processing")
            {
                return true;
            }

            if (unresolvedEvent.Status == "dead-letter" &&
                IncrementalDiscoveryEventPayload.TryParse(unresolvedEvent.Data, out var payload) &&
                payload is { HasWindow: true } &&
                payload.LastUpdatedPost == lastUpdatedPost)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<List<int>> RecoverLegacyDiscoveryEventsAsync(bool apply, CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var pendingEvents = await context.PipelineEvents
            .Where(e => e.EventType == "studies.discovered" && e.Status == "pending")
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var legacyEvents = pendingEvents
            .Where(e => IncrementalDiscoveryEventPayload.TryParse(e.Data, out var payload) &&
                        payload is { HasWindow: false })
            .ToList();

        if (apply && legacyEvents.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var @event in legacyEvents)
            {
                @event.Status = "completed";
                @event.CompletedAt = now;
                @event.ErrorMessage = "Superseded by full-corpus backfill recovery.";
                @event.ClaimedBy = null;
                @event.ClaimedAt = null;
                @event.UpdatedAt = now;
            }

            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return legacyEvents.Select(e => e.Id).ToList();
    }

    private async Task<List<(string Status, string? Data)>> GetUnresolvedEventDataAsync(
        string eventType,
        CancellationToken ct)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var rows = await context.PipelineEvents
            .Where(e => e.EventType == eventType && e.Status != "completed")
            .Select(e => new { e.Status, e.Data })
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows.Select(e => (e.Status, e.Data)).ToList();
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

    public async Task UpdateEventProgressAsync(int eventId, int processed, int total, CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        // Only claimed events may carry progress; a released or completed event
        // (e.g., after a claim timeout) must not be resurrected by a late write.
        await context.PipelineEvents
            .Where(e => e.Id == eventId && e.Status == "processing")
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.ProgressProcessed, processed)
                    .SetProperty(e => e.ProgressTotal, total)
                    .SetProperty(e => e.ProgressUpdatedAt, DateTime.UtcNow)
                    .SetProperty(e => e.UpdatedAt, DateTime.UtcNow),
                ct)
            .ConfigureAwait(false);
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
                .OrderByDescending(e => e.CompletedAt)
                .Take(DurationSampleLimit)
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
            .OrderByDescending(e => e.CompletedAt)
            .Take(DurationSampleLimit)
            .Select(e => new { e.EventType, Ms = (e.CompletedAt!.Value - e.ClaimedAt!.Value).TotalMilliseconds })
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var durationsByType = allDurations
            .GroupBy(d => d.EventType)
            .ToDictionary(g => g.Key, g => g.Select(d => d.Ms).ToList());

        var heartbeatCutoff15m = DateTime.UtcNow.AddMinutes(-15);
        var heartbeatCutoff1h = DateTime.UtcNow.AddHours(-1);
        var heartbeats = await context.PipelineEvents
            .Where(e => e.Status == "completed" && e.CompletedAt.HasValue && e.CompletedAt >= heartbeatCutoff1h)
            .Select(e => new { e.EventType, e.CompletedAt })
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var inFlightEvents = await context.PipelineEvents
            .Where(e => e.Status == "processing" && e.ClaimedAt.HasValue)
            .OrderByDescending(e => e.ClaimedAt)
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var inFlightByType = inFlightEvents
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => g.First());

        var now = DateTime.UtcNow;
        return groups.Select(r =>
        {
            List<double>? durations = null;
            durationsByType.TryGetValue(r.EventType, out durations);

            var typeHeartbeats = heartbeats.Where(h => h.EventType == r.EventType).ToList();
            var inFlightEntity = inFlightByType.GetValueOrDefault(r.EventType);
            var (percent, ratePerMin, etaUtc) = inFlightEntity is null
                ? ((double?)null, (double?)null, (DateTime?)null)
                : InFlightProgress.Calculate(
                    inFlightEntity.ProgressProcessed,
                    inFlightEntity.ProgressTotal,
                    inFlightEntity.ClaimedAt,
                    now);

            return new EventTypeBreakdown
            {
                EventType = r.EventType,
                Pending = r.Pending,
                Processing = r.Processing,
                Completed = r.Completed,
                Failed = r.Failed,
                DeadLetter = r.DeadLetter,
                AverageProcessingTimeMs = r.AvgProcessingMs,
                Percentiles = durations is { Count: > 0 } ? DurationPercentileCalculator.ComputePercentiles(durations) : null,
                CompletedLast15m = typeHeartbeats.Count(h => h.CompletedAt >= heartbeatCutoff15m),
                CompletedLast1h = typeHeartbeats.Count,
                InFlight = inFlightEntity is null ? null : new InFlightEventInfo
                {
                    EventId = inFlightEntity.Id,
                    ClaimedAt = inFlightEntity.ClaimedAt!.Value,
                    ProgressUpdatedAt = inFlightEntity.ProgressUpdatedAt,
                    Processed = inFlightEntity.ProgressProcessed,
                    Total = inFlightEntity.ProgressTotal,
                    Percent = percent,
                    RatePerMin = ratePerMin,
                    EtaUtc = etaUtc
                }
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

    public async Task<int> RetryAllDeadLetterEventsAsync(string? eventType = null, CancellationToken ct = default)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString)
                .Options);

        var query = context.PipelineEvents.Where(e => e.Status == "dead-letter");
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        var events = await query.ToListAsync(ct).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        foreach (var @event in events)
        {
            @event.Status = "pending";
            @event.RetryCount = 0;
            @event.ErrorMessage = null;
            @event.ClaimedBy = null;
            @event.ClaimedAt = null;
            @event.UpdatedAt = now;
        }

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return events.Count;
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

        // Backfill chunks run for hours; use their longer timeout while all other
        // event types use the short timeout used by their worker loops.
        var now = DateTime.UtcNow;
        var ordinaryThreshold = now - claimTimeout;
        var backfillThreshold = now - _backfillClaimTimeout;

        var stuckEvents = await context.PipelineEvents
            .Where(e => e.Status == "processing"
                        && e.ClaimedAt.HasValue
                        && ((e.EventType == "studies.backfill" && e.ClaimedAt < backfillThreshold)
                            || (e.EventType != "studies.backfill" && e.ClaimedAt < ordinaryThreshold)))
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
