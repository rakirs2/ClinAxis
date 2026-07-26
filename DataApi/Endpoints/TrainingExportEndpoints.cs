using Microsoft.EntityFrameworkCore;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace DataApi.Endpoints;

internal static class TrainingExportEndpoints
{
    internal static void MapTrainingExportEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/api/export/training/keywords", async (HttpResponse response) =>
        {
            using var ctx = new ClinicalTrialsContext(new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(connectionString).Options);

            response.ContentType = "text/csv";
            response.Headers["Content-Disposition"] = "attachment; filename=\"training-keywords.csv\"";
            await response.WriteAsync("value,label,study_nct_id\n");

            await foreach (var k in ctx.StudyKeywords
                .Select(k => new { Value = k.Keyword, Label = 1, k.StudyNctId })
                .OrderBy(k => k.StudyNctId)
                .AsAsyncEnumerable())
            {
                var escaped = k.Value.Replace("\"", "\"\"", StringComparison.Ordinal);
                await response.WriteAsync($"\"{escaped}\",{k.Label},{k.StudyNctId}\n");
            }

            await foreach (var r in ctx.RejectedEntities
                .Where(r => r.EntityType == "keyword")
                .Select(r => new { r.Value, Label = 0, r.StudyNctId })
                .OrderBy(r => r.StudyNctId)
                .AsAsyncEnumerable())
            {
                var escaped = r.Value.Replace("\"", "\"\"", StringComparison.Ordinal);
                await response.WriteAsync($"\"{escaped}\",{r.Label},{r.StudyNctId}\n");
            }
        });

        app.MapGet("/api/export/training/names", async (HttpResponse response) =>
        {
            using var ctx = new ClinicalTrialsContext(new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(connectionString).Options);

            response.ContentType = "text/csv";
            response.Headers["Content-Disposition"] = "attachment; filename=\"training-names.csv\"";
            await response.WriteAsync("value,label,study_nct_id\n");

            await foreach (var n in ctx.InvestigatorPersons
                .Join(ctx.StudyInvestigators,
                    p => p.Id,
                    si => si.InvestigatorPersonId,
                    (p, si) => new { Value = p.FullName, Label = 1, si.StudyNctId })
                .OrderBy(n => n.StudyNctId)
                .AsAsyncEnumerable())
            {
                var escaped = n.Value.Replace("\"", "\"\"", StringComparison.Ordinal);
                await response.WriteAsync($"\"{escaped}\",{n.Label},{n.StudyNctId}\n");
            }

            await foreach (var r in ctx.RejectedEntities
                .Where(r => r.EntityType == "investigator_name")
                .Select(r => new { r.Value, Label = 0, r.StudyNctId })
                .OrderBy(r => r.StudyNctId)
                .AsAsyncEnumerable())
            {
                var escaped = r.Value.Replace("\"", "\"\"", StringComparison.Ordinal);
                await response.WriteAsync($"\"{escaped}\",{r.Label},{r.StudyNctId}\n");
            }
        });

        app.MapGet("/api/export/training/conditions", async (HttpResponse response) =>
        {
            using var ctx = new ClinicalTrialsContext(new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(connectionString).Options);

            response.ContentType = "text/csv";
            response.Headers["Content-Disposition"] = "attachment; filename=\"training-conditions.csv\"";
            await response.WriteAsync("value,label,study_nct_id\n");

            await foreach (var c in ctx.StudyConditions
                .Include(c => c.MeshDescriptor)
                .Select(c => new { Value = c.MeshDescriptor != null ? c.MeshDescriptor.Name : "", Label = 1, c.StudyNctId })
                .OrderBy(c => c.StudyNctId)
                .AsAsyncEnumerable())
            {
                var escaped = c.Value.Replace("\"", "\"\"", StringComparison.Ordinal);
                await response.WriteAsync($"\"{escaped}\",{c.Label},{c.StudyNctId}\n");
            }

            await foreach (var r in ctx.RejectedEntities
                .Where(r => r.EntityType == "condition")
                .Select(r => new { r.Value, Label = 0, r.StudyNctId })
                .OrderBy(r => r.StudyNctId)
                .AsAsyncEnumerable())
            {
                var escaped = r.Value.Replace("\"", "\"\"", StringComparison.Ordinal);
                await response.WriteAsync($"\"{escaped}\",{r.Label},{r.StudyNctId}\n");
            }
        });

        app.MapGet("/api/export/training/affiliations", async (HttpResponse response) =>
        {
            using var ctx = new ClinicalTrialsContext(new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(connectionString).Options);

            response.ContentType = "text/csv";
            response.Headers["Content-Disposition"] = "attachment; filename=\"training-affiliations.csv\"";
            await response.WriteAsync("value,label,study_nct_id\n");

            await foreach (var a in ctx.InvestigatorAffiliations
                .Join(ctx.StudyInvestigators,
                    af => af.InvestigatorPersonId,
                    si => si.InvestigatorPersonId,
                    (af, si) => new { Value = af.InstitutionName, Label = 1, si.StudyNctId })
                .OrderBy(a => a.StudyNctId)
                .AsAsyncEnumerable())
            {
                var escaped = a.Value.Replace("\"", "\"\"", StringComparison.Ordinal);
                await response.WriteAsync($"\"{escaped}\",{a.Label},{a.StudyNctId}\n");
            }

            await foreach (var r in ctx.RejectedEntities
                .Where(r => r.EntityType == "affiliation")
                .Select(r => new { r.Value, Label = 0, r.StudyNctId })
                .OrderBy(r => r.StudyNctId)
                .AsAsyncEnumerable())
            {
                var escaped = r.Value.Replace("\"", "\"\"", StringComparison.Ordinal);
                await response.WriteAsync($"\"{escaped}\",{r.Label},{r.StudyNctId}\n");
            }
        });
    }
}
