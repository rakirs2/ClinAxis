using Microsoft.Extensions.Caching.Memory;
using Scrapers.Services.EventQueue;

namespace DataApi.Endpoints;

internal static class EventQueueEndpoints
{
    private const string StatsCacheKey = "event_queue_stats_response";
    private static readonly TimeSpan StatsCacheDuration = TimeSpan.FromSeconds(15);
    private static readonly SemaphoreSlim StatsCacheLock = new(1, 1);

    internal static void MapEventQueueEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/api/event-queue/stats", async (IMemoryCache cache, CancellationToken ct) =>
        {
            if (cache.TryGetValue(StatsCacheKey, out object? cached) && cached is not null)
            {
                return Results.Ok(cached);
            }

            await StatsCacheLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (cache.TryGetValue(StatsCacheKey, out cached) && cached is not null)
                {
                    return Results.Ok(cached);
                }

                var eventQueueService = new EventQueueService(connectionString);
                Task<EventQueueStats> statsTask = eventQueueService.GetStatsAsync(ct);
                Task<List<EventTypeBreakdown>> breakdownTask = eventQueueService.GetEventTypeBreakdownAsync(ct);
                await Task.WhenAll(statsTask, breakdownTask).ConfigureAwait(false);
                var stats = await statsTask.ConfigureAwait(false);
                var byEventType = await breakdownTask.ConfigureAwait(false);
                var response = new
                {
                    stats.PendingCount,
                    stats.ProcessingCount,
                    stats.CompletedCount,
                    stats.DeadLetterCount,
                    stats.FailedCount,
                    stats.AverageProcessingTimeMs,
                    stats.FailureRate,
                    stats.EstimatedTimeRemainingMs,
                    byEventType = byEventType.Select(et => new
                    {
                        et.EventType,
                        et.Pending,
                        et.Processing,
                        et.Completed,
                        et.Failed,
                        et.DeadLetter,
                        et.AverageProcessingTimeMs,
                        et.CompletedLast15m,
                        et.CompletedLast1h,
                        inFlight = et.InFlight != null ? new
                        {
                            et.InFlight.EventId,
                            et.InFlight.ClaimedAt,
                            et.InFlight.ProgressUpdatedAt,
                            et.InFlight.Processed,
                            et.InFlight.Total,
                            et.InFlight.Percent,
                            et.InFlight.RatePerMin,
                            et.InFlight.EtaUtc
                        } : null,
                        percentiles = et.Percentiles != null ? new
                        {
                            et.Percentiles.Count,
                            et.Percentiles.MinMs,
                            et.Percentiles.P50Ms,
                            et.Percentiles.P95Ms,
                            et.Percentiles.P99Ms,
                            et.Percentiles.MaxMs
                        } : null
                    })
                };

                cache.Set(StatsCacheKey, response, StatsCacheDuration);
                return Results.Ok(response);
            }
            finally
            {
                StatsCacheLock.Release();
            }
        });

        app.MapGet("/api/event-queue/duration-history", async (string? eventType, string? period, int? bucketMinutes) =>
        {
            var eventQueueService = new EventQueueService(connectionString);
            var history = await eventQueueService.GetDurationHistoryAsync(
                eventType, period ?? "24h", bucketMinutes ?? 60);
            return Results.Ok(history.Select(h => new
            {
                h.Bucket,
                h.Count,
                h.AverageMs,
                h.P50Ms,
                h.P95Ms,
                h.P99Ms
            }));
        });

        app.MapGet("/api/event-queue/dead-letter", async (int? page, int? pageSize) =>
        {
            var eventQueueService = new EventQueueService(connectionString);
            var p = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 100, 1, 200);
            var (deadLetterEvents, total) = await eventQueueService.GetDeadLetterEventsPagedAsync(p, ps);
            return Results.Ok(new
            {
                data = deadLetterEvents.Select(e => new
                {
                    e.Id,
                    e.EventType,
                    e.Data,
                    e.Status,
                    e.ErrorMessage,
                    e.RetryCount,
                    e.CreatedAt,
                    e.CompletedAt
                }),
                total,
                page = p,
                pageSize = ps,
                totalPages = (int)Math.Ceiling((double)total / ps)
            });
        });

        app.MapPost("/api/event-queue/dead-letter/{eventId:int}/retry", async (int eventId) =>
        {
            var eventQueueService = new EventQueueService(connectionString);
            var result = await eventQueueService.RetryEventAsync(eventId);
            return result ? Results.Ok() : Results.NotFound();
        });

        app.MapPost("/api/event-queue/dead-letter/retry-all", async (string? eventType) =>
        {
            var eventQueueService = new EventQueueService(connectionString);
            var resetCount = await eventQueueService.RetryAllDeadLetterEventsAsync(eventType);
            return Results.Ok(new { resetCount, eventType });
        });

        app.MapPost("/api/event-queue/dead-letter/{eventId:int}/ignore", async (int eventId) =>
        {
            var eventQueueService = new EventQueueService(connectionString);
            var result = await eventQueueService.IgnoreEventAsync(eventId);
            return result ? Results.Ok() : Results.NotFound();
        });

        app.MapPost("/api/event-queue/recovery/legacy-discovered", async (bool? apply) =>
        {
            var eventQueueService = new EventQueueService(connectionString);
            var shouldApply = apply == true;
            var eventIds = await eventQueueService.RecoverLegacyDiscoveryEventsAsync(shouldApply);
            return Results.Ok(new
            {
                applied = shouldApply,
                eligibleCount = eventIds.Count,
                eventIds
            });
        });
    }
}
