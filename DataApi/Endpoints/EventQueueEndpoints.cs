using DataApi.Services;
using Scrapers.Services.EventQueue;

namespace DataApi.Endpoints;

internal static class EventQueueEndpoints
{
    internal static void MapEventQueueEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/api/event-queue/stats", (EventQueueTelemetryCache telemetry) =>
        {
            return Results.Ok(telemetry.Current);
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
