using Microsoft.EntityFrameworkCore;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace DataApi.Endpoints;

internal static class PipelineEndpoints
{
    internal static void MapPipelineEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/api/rejected-names", async () =>
        {
            using var ctx = new ClinicalTrialsContext(new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(connectionString).Options);
            var names = await ctx.Set<RejectedInvestigatorNameEntity>()
                .OrderByDescending(n => n.OccurrenceCount)
                .Select(n => new
                {
                    id = n.Id,
                    name = n.FullName,
                    occurrenceCount = n.OccurrenceCount,
                    studyCount = n.StudyCount,
                    rejectionReason = n.RejectionReason,
                    isHumanOverride = n.IsHumanOverride,
                    note = n.Note
                })
                .ToListAsync();
            return Results.Ok(names);
        });

        app.MapGet("/api/rejected-entities", async (string type, int? page, int? pageSize,
            string? reason, string? role, string? affiliation, string? nctId) =>
        {
            var repo = new StudyRepository(connectionString);
            var p = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 50, 1, 200);
            var (items, total) = await repo.GetRejectedEntitiesPagedAsync(type, p, ps, reason, role, affiliation, nctId);
            return Results.Ok(new
            {
                data = items.Select(e => new
                {
                    e.Id,
                    e.EntityType,
                    e.Value,
                    e.StudyNctId,
                    e.Role,
                    e.Affiliation,
                    e.RejectionReason,
                    e.RejectedAt
                }),
                total,
                page = p,
                pageSize = ps,
                totalPages = (int)Math.Ceiling((double)total / ps)
            });
        });
        app.MapPost("/api/rejected-names/{id:guid}/override", async (Guid id, RejectedNameOverrideRequest request) =>
        {
            var repo = new StudyRepository(connectionString);
            var updated = await repo.SetRejectedInvestigatorNameOverrideAsync(id, request.IsHumanOverride, request.Note);
            if (!updated)
            {
                return Results.NotFound();
            }

            using var ctx = new ClinicalTrialsContext(new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(connectionString).Options);
            var entity = await ctx.Set<RejectedInvestigatorNameEntity>().FirstAsync(n => n.Id == id);
            return Results.Ok(new
            {
                id = entity.Id,
                name = entity.FullName,
                occurrenceCount = entity.OccurrenceCount,
                studyCount = entity.StudyCount,
                rejectionReason = entity.RejectionReason,
                isHumanOverride = entity.IsHumanOverride,
                note = entity.Note
            });
        });
    }

    private sealed record RejectedNameOverrideRequest(bool IsHumanOverride, string? Note);
}
