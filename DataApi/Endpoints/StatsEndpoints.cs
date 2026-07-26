using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services.EventQueue;

namespace DataApi.Endpoints;

internal static class StatsEndpoints
{
    internal static void MapStatsEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/api/stats", async () =>
        {
            var repo = new StudyRepository(connectionString);
            var studies = await repo.CountStudiesAsync();
            var investigators = await repo.CountInvestigatorsAsync();
            var pubmedPapers = await repo.CountPubmedPapersAsync();
            var keywords = await repo.CountKeywordsAsync();
            return Results.Ok(new
            {
                totalStudies = studies,
                totalInvestigators = investigators,
                totalPubmedPapers = pubmedPapers,
                totalKeywords = keywords
            });
        });

        app.MapGet("/api/stats/status-breakdown", async () =>
        {
            var repo = new StudyRepository(connectionString);
            var breakdown = await repo.GetStatusBreakdownAsync();
            return Results.Ok(breakdown);
        });

        app.MapGet("/api/telemetry", async () =>
        {
            var repo = new StudyRepository(connectionString);

            var studies = await repo.CountStudiesAsync();
            var investigators = await repo.CountInvestigatorsAsync();
            var pubmedPapers = await repo.CountPubmedPapersAsync();
            var keywords = await repo.CountKeywordsAsync();

            List<PipelineRunEntity> recentRuns = await repo.GetPipelineRunsAsync(1, 5);
            IReadOnlyList<ScrapeEventEntity> recentEvents = await repo.GetRecentScrapeEventsAsync(20);

            var totalInvestigatorsForCoverage = investigators > 0 ? investigators : 1;
            var withNpi = await repo.CountInvestigatorsWithNpiAsync();
            var notFound = await repo.CountInvestigatorsByEnrichmentResultAsync("not_found");
            var ambiguous = await repo.CountInvestigatorsByEnrichmentResultAsync("ambiguous");
            var notAttempted = await repo.CountInvestigatorsNotAttemptedAsync();

            return Results.Ok(new
            {
                db = new
                {
                    totalStudies = studies,
                    totalInvestigators = investigators,
                    totalPubmedPapers = pubmedPapers,
                    totalKeywords = keywords
                },
                enrichment = new
                {
                    totalInvestigators = investigators,
                    withNpi,
                    notFound,
                    ambiguous,
                    notAttempted,
                    npiCoveragePct = Math.Round((double)withNpi / totalInvestigatorsForCoverage * 100, 1)
                },
                pipelineRuns = recentRuns.Select(r => StudyMapper.ToPipelineRun(r)),
                recentEvents = recentEvents.Select(e => new
                {
                    id = e.Id,
                    pipelineRunId = e.PipelineRunId,
                    timestamp = e.Timestamp,
                    source = e.Source,
                    eventType = e.EventType,
                    level = e.Level,
                    durationMs = e.DurationMs,
                    recordsAffected = e.RecordsAffected,
                    message = e.Message,
                    httpStatusCode = e.HttpStatusCode
                }),
            });
        });

        app.MapGet("/api/aggregations", async () =>
        {
            var repo = new StudyRepository(connectionString);
            var piCount = await repo.CountPiAggregationsAsync();
            IReadOnlyList<CategoryTypeCount> categoryByType = await repo.CountCategoryAggregationsByTypeAsync();
            Dictionary<string, int> categoryDict = new();
            foreach (CategoryTypeCount c in categoryByType)
            {
                categoryDict[c.CategoryType] = c.Count;
            }
            return Results.Ok(new
            {
                piAggregationCount = piCount,
                categoryAggregationCount = categoryByType.Sum(c => c.Count),
                categoryAggregationsByType = categoryDict
            });
        });

        app.MapGet("/api/database/size", async () =>
        {
            var repo = new StudyRepository(connectionString);
            var tables = await repo.GetTableRowCountsAsync();
            var totalRows = tables.Sum(t => t.RowCount);
            return Results.Ok(new
            {
                totalRowCount = totalRows,
                estimatedSizeGb = Math.Round(totalRows / 1048576.0, 2),
                tables
            });
        });

        app.MapGet("/api/scraper-progress", async (IMemoryCache cache) =>
        {
            var cacheKey = "scraper_progress";
            if (cache.TryGetValue(cacheKey, out object? cached) && cached is not null)
                return Results.Ok(cached);

            int totalAvailable = 0;
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var uri = new Uri("https://clinicaltrials.gov/api/v2/studies?format=json&pageSize=1&countTotal=true");
                var json = await httpClient.GetStringAsync(uri);
                using var doc = JsonDocument.Parse(json);
                totalAvailable = doc.RootElement.GetProperty("totalCount").GetInt32();
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }
            catch (JsonException)
            {
            }

            var repo = new StudyRepository(connectionString);
            var totalInDb = await repo.CountStudiesAsync();

            var result = new
            {
                totalAvailable,
                totalInDb,
                percentScraped = totalAvailable > 0 ? Math.Round((double)totalInDb / totalAvailable * 100, 1) : 0.0,
                lastChecked = DateTime.UtcNow
            };

            var ttl = totalAvailable > 0 ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
            cache.Set(cacheKey, result, ttl);
            return Results.Ok(result);
        });

        app.MapGet("/api/enrichment/breakdown", async () =>
        {
            var repo = new StudyRepository(connectionString);
            var breakdown = await repo.GetNpiEnrichmentBreakdownAsync();
            return Results.Ok(breakdown);
        });

        app.MapGet("/api/data-source-state", async () =>
        {
            var dataSourceService = new DataSourceStateService(connectionString);
            var states = await dataSourceService.GetAllStatesAsync();
            return Results.Ok(states.Select(s => new
            {
                s.SourceName,
                s.LastSyncTimestamp,
                s.Status,
                s.ErrorMessage,
                s.UpdatedAt,
                s.RejectedKeywordsTotal,
                s.NextScheduledRun
            }));
        });
    }
}
