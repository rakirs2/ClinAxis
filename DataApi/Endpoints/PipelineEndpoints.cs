using Microsoft.EntityFrameworkCore;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace DataApi.Endpoints;

internal static class PipelineEndpoints
{
    internal static void MapPipelineEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/api/pipeline-runs", async (int? page, int? pageSize) =>
        {
            var repo = new StudyRepository(connectionString);
            var p = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 20, 1, 100);
            List<PipelineRunEntity> runs = await repo.GetPipelineRunsAsync(p, ps);
            return Results.Ok(new { data = runs.Select(r => StudyMapper.ToPipelineRun(r)) });
        });

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

        app.MapGet("/api/rejected-entities", async (string type, int? page, int? pageSize) =>
        {
            var repo = new StudyRepository(connectionString);
            var p = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 50, 1, 200);
            var (items, total) = await repo.GetRejectedEntitiesPagedAsync(type, p, ps);
            return Results.Ok(new
            {
                data = items.Select(e => new
                {
                    e.Id,
                    e.EntityType,
                    e.Value,
                    e.StudyNctId,
                    e.RejectedAt
                }),
                total,
                page = p,
                pageSize = ps,
                totalPages = (int)Math.Ceiling((double)total / ps)
            });
        });
    }
}
