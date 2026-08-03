using DataApi.Models;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Utilities;

namespace DataApi.Endpoints;

internal static class PageViewsEndpoints
{
    internal const int MaxPathLength = 500;
    internal const int MaxSessionIdLength = 36;

    internal static void MapPageViewsEndpoints(this WebApplication app, string connectionString)
    {
        app.MapPost("/api/page-views", async (PageViewRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Path))
            {
                return Results.BadRequest(new { error = "Path is required" });
            }
            if (string.IsNullOrWhiteSpace(request?.SessionId))
            {
                return Results.BadRequest(new { error = "SessionId is required" });
            }
            if (request!.Path.Length > MaxPathLength)
            {
                return Results.BadRequest(new { error = $"Path must be {MaxPathLength} characters or fewer" });
            }
            if (request.SessionId.Length > MaxSessionIdLength)
            {
                return Results.BadRequest(new { error = $"SessionId must be {MaxSessionIdLength} characters or fewer" });
            }

            var repo = new StudyRepository(connectionString);
            var view = new PageViewEntity
            {
                Path = request.Path,
                SessionId = request.SessionId,
                ViewedAt = request.ViewedAt ?? DateTime.UtcNow
            };
            await repo.AddPageViewAsync(view);
            return Results.Ok(new { id = view.Id });
        });

        app.MapGet("/api/page-views/stats", async (string? period) =>
        {
            var nowUtc = DateTime.UtcNow;
            if (!PageViewStatsAggregator.TryParsePeriod(period, nowUtc, out var fromUtc))
            {
                return Results.BadRequest(new { error = "period must be one of: day, week, month" });
            }

            var repo = new StudyRepository(connectionString);
            var rows = await repo.GetPageViewsAsync(fromUtc);
            var stats = PageViewStatsAggregator.Aggregate(rows);
            return Results.Ok(new
            {
                totalViews = stats.TotalViews,
                uniqueVisitors = stats.UniqueVisitors,
                topPages = stats.TopPages.Select(p => new { path = p.Path, count = p.Count })
            });
        });
    }
}