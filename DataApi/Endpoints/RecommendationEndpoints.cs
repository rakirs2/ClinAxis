using DataApi.Models;
using DataApi.Services;

namespace DataApi.Endpoints;

internal static class RecommendationEndpoints
{
    internal static void MapRecommendationEndpoints(this WebApplication app, string connectionString)
    {
        app.MapPost("/api/recommend/investigators", async (RecommendRequest request) =>
        {
            if (request.GetAllPrefixes().Count == 0)
            {
                return Results.BadRequest(new { error = "At least one therapy or condition tree prefix is required" });
            }

            var service = new RecommendationService(connectionString);
            return Results.Ok(await service.RecommendAsync(request));
        });
    }
}
