using DataApi.Models;
using DataApi.Services;

namespace DataApi.Endpoints;

internal static class InvestigatorFinderEndpoints
{
    internal static void MapInvestigatorFinderEndpoints(this WebApplication app, string connectionString)
    {
        app.MapPost("/api/investigator-finder", async (FinderRequest request) =>
        {
            if ((request.ConditionTreePrefixes == null || request.ConditionTreePrefixes.Count == 0) &&
                (request.DrugTreePrefixes == null || request.DrugTreePrefixes.Count == 0) &&
                (request.TherapyTreePrefixes == null || request.TherapyTreePrefixes.Count == 0))
            {
                return Results.BadRequest(new { error = "At least one tree prefix is required" });
            }

            var service = new InvestigatorFinderService(connectionString);
            var result = await service.FindAsync(request);
            return Results.Ok(result);
        });
    }
}
