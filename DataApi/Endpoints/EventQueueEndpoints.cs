using Scrapers.Services.EventQueue;

namespace DataApi.Endpoints;

internal static class EventQueueEndpoints
{
    internal static void MapEventQueueEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/api/event-queue/stats", async () =>
        {
            var eventQueueService = new EventQueueService(connectionString);
            var stats = await eventQueueService.GetStatsAsync();
            var byEventType = await eventQueueService.GetEventTypeBreakdownAsync();
            return Results.Ok(new
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
            });
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

        app.MapPost("/api/event-queue/dead-letter/{eventId:int}/ignore", async (int eventId) =>
        {
            var eventQueueService = new EventQueueService(connectionString);
            var result = await eventQueueService.IgnoreEventAsync(eventId);
            return result ? Results.Ok() : Results.NotFound();
        });
    }
}
