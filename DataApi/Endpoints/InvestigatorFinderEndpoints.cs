using DataApi.Models;
using DataApi.Services;

namespace DataApi.Endpoints;

internal static class InvestigatorFinderEndpoints
{
    internal static void MapInvestigatorFinderEndpoints(this WebApplication app, string connectionString, PiCompletionModel? piCompletionModel)
    {
        app.MapPost("/api/investigator-finder", async (FinderRequest request) =>
        {
            if (request.GetAllPrefixes().Count == 0)
            {
                return Results.BadRequest(new { error = "At least one tree prefix is required" });
            }

            var service = new InvestigatorFinderService(connectionString, piCompletionModel);
            var result = await service.FindAsync(request);
            return Results.Ok(result);
        });
    }
}
