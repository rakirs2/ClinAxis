using Microsoft.EntityFrameworkCore;
using Scrapers;
using Scrapers.Persistence;

namespace DataApi.Endpoints;

internal static class ExportEndpoints
{
    internal static void MapExportEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/api/export/keywords", async (HttpResponse response) =>
        {
            using var ctx = new ClinicalTrialsContext(new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(connectionString).Options);

            var keywords = await ctx.StudyKeywords
                .GroupBy(k => k.Keyword)
                .Select(g => new { Keyword = g.Key, StudyCount = g.Count() })
                .OrderByDescending(k => k.StudyCount)
                .ToListAsync();

            response.ContentType = "text/csv";
            response.Headers["Content-Disposition"] = "attachment; filename=\"keywords-export.csv\"";

            await response.WriteAsync("keyword,study_count\n");
            foreach (var k in keywords)
            {
                var escaped = k.Keyword.Replace("\"", "\"\"", StringComparison.Ordinal);
                await response.WriteAsync($"\"{escaped}\",{k.StudyCount}\n");
            }
        });
    }
}
