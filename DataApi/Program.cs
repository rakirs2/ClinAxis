using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using DataApi;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5003");

var connectionString = ConnectionStringProvider.WithPoolLimits(ConnectionStringProvider.Default);

// Retry database connection during startup to handle transient DB delays
var startupRepo = new StudyRepository(connectionString);

// Reset database on demand via --reset-db CLI flag
if (args.Contains("--reset-db"))
{
    await startupRepo.ResetDatabaseAsync();
    await Console.Out.WriteLineAsync("Database reset complete. Starting normally.");
}

var maxRetries = 5;
var retryDelay = TimeSpan.FromSeconds(3);
for (int attempt = 1; attempt <= maxRetries; attempt++)
{
    try
    {
        await startupRepo.MigrateSchemaAsync();
        break;
    }
    catch (Exception ex) when (attempt < maxRetries)
    {
        await Console.Error.WriteLineAsync($"Database connection failed (attempt {attempt}/{maxRetries}): {ex.Message}");
        await Task.Delay(retryDelay);
    }
}

builder.Services.AddHealthChecks();
builder.Services.AddMemoryCache();

WebApplication app = builder.Build();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var assembly = typeof(Program).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0.0";
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;
        var response = new
        {
            status = report.Status.ToString(),
            application = "DataApi",
            version,
            informationalVersion = infoVersion,
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        };
        context.Response.ContentType = "application/json";
        await System.Text.Json.JsonSerializer.SerializeAsync(context.Response.Body, response).ConfigureAwait(false);
    }
});

// Endpoints for advanced search filter options
app.MapGet("/api/distinct-conditions", async (IMemoryCache cache) =>
{
    var cacheKey = "conditions_list";
    if (cache.TryGetValue(cacheKey, out List<string>? conditions) && conditions is not null)
    {
        return Results.Ok(conditions);
    }

    var repo = new StudyRepository(connectionString);
    conditions = await repo.GetDistinctConditionsAsync();
    var ttl = TimeSpan.FromMinutes(app.Configuration.GetValue<int>("CacheSettings:ConditionsCacheDurationMinutes", 5));
    cache.Set(cacheKey, conditions, ttl);
    return Results.Ok(conditions);
});

app.MapGet("/api/distinct-locations", async (IMemoryCache cache, string? country, string? state, string? city) =>
{
    var cacheKey = $"locations_{country ?? ""}_{state ?? ""}_{city ?? ""}";
    if (cache.TryGetValue(cacheKey, out object? cached) && cached is not null)
    {
        return Results.Ok(cached);
    }

    var repo = new StudyRepository(connectionString);
    var (countries, states, cities, facilities) = await repo.GetDistinctLocationsAsync(country, state, city);
    var result = new { countries, states, cities, facilities };
    var ttl = TimeSpan.FromMinutes(app.Configuration.GetValue<int>("CacheSettings:LocationsCacheDurationMinutes", 5));
    cache.Set(cacheKey, result, ttl);
    return Results.Ok(result);
});

app.MapGet("/api/studies", async (
    int? page, int? pageSize,
    string? keyword,
    string? status, string? phase,
    string? condition,
    string? country, string? state, string? city, string? facility,
    int? enrollmentMin, int? enrollmentMax,
    DateTime? startDateFrom, DateTime? startDateTo) =>
{
    var repo = new StudyRepository(connectionString);
    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 10, 1, 100);

    // Build search criteria from query parameters
    var criteria = new StudySearchCriteria
    {
        Keyword = keyword,
        Statuses = ParseCsvParam(status),
        Phases = ParseCsvParam(phase),
        Conditions = ParseCsvParam(condition),
        Countries = ParseCsvParam(country),
        States = ParseCsvParam(state),
        Cities = ParseCsvParam(city),
        Facilities = ParseCsvParam(facility),
        EnrollmentMin = enrollmentMin,
        EnrollmentMax = enrollmentMax,
        StartDateFrom = startDateFrom,
        StartDateTo = startDateTo,
        Page = p,
        PageSize = ps
    };

    var studies = await repo.SearchStudiesAsync(criteria);
    var total = await repo.CountStudiesFilteredAsync(criteria);

    return Results.Ok(new
    {
        data = studies.Select(s => StudyMapper.ToSummary(s)),
        total,
        page = p,
        pageSize = ps,
        totalPages = (int)Math.Ceiling((double)total / ps)
    });
});

app.MapGet("/api/studies/{nctId}", async (string nctId) =>
{
    var repo = new StudyRepository(connectionString);
    StudyEntity? study = await repo.GetStudyByNctIdAsync(nctId);
    return study is null ? Results.NotFound(new { error = "Study not found" }) : Results.Ok(StudyMapper.ToDetail(study));
});

app.MapGet("/api/investigators", async (int? page, int? pageSize, string? search, bool? hasNpi) =>
{
    var repo = new StudyRepository(connectionString);
    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 10, 1, 100);

    var persons = await repo.GetInvestigatorPersonsPagedAsync(p, ps, search, hasNpi);
    var total = await repo.CountInvestigatorPersonsFilteredAsync(search, hasNpi);

    return Results.Ok(new
    {
        data = persons.Select(p => new
        {
            uuid = p.Uuid,
            name = p.Name,
            orcid = p.Orcid,
            ncbiId = p.NcbiId,
            npi = p.Npi,
            studyCount = p.StudyCount,
            paperCount = p.PaperCount,
            primaryAffiliation = p.PrimaryAffiliation
        }),
        total,
        page = p,
        pageSize = ps,
        totalPages = (int)Math.Ceiling((double)total / ps)
    });
});

// Investigator detail endpoint (new person model)
app.MapGet("/api/investigators/{uuid}", async (Guid uuid) =>
{
    var repo = new StudyRepository(connectionString);
    var person = await repo.GetInvestigatorPersonByUuidAsync(uuid);
    
    if (person == null)
    {
        return Results.NotFound(new { message = "Investigator not found" });
    }

    var criteria = new StudySearchCriteria
    {
        Page = 1,
        PageSize = int.MaxValue
    };
    var studies = await repo.GetStudiesByInvestigatorPersonIdAsync(uuid, criteria);

    var detail = InvestigatorMapper.ToDetail(person, studies);
    return Results.Ok(detail);
});

// Investigator studies endpoint with filters, sorting, and pagination
app.MapGet("/api/investigators/{uuid}/studies", async (
    Guid uuid,
    int? page, int? pageSize,
    string? search, string? status, string? phase,
    string? condition,
    int? enrollmentMin, int? enrollmentMax,
    DateTime? startDateFrom, DateTime? startDateTo,
    string? sort, string? order) =>
{
    var repo = new StudyRepository(connectionString);
    var person = await repo.GetInvestigatorPersonByUuidAsync(uuid);
    
    if (person == null)
    {
        return Results.NotFound(new { message = "Investigator not found" });
    }

    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 10, 1, 100);

    // Build StudySearchCriteria from query parameters
    var criteria = new StudySearchCriteria
    {
        Keyword = search,
        Statuses = string.IsNullOrEmpty(status) ? null : status.Split(',').Select(s => s.Trim()).ToList(),
        Phases = string.IsNullOrEmpty(phase) ? null : phase.Split(',').Select(p => p.Trim()).ToList(),
        Conditions = string.IsNullOrEmpty(condition) ? null : condition.Split(',').Select(c => c.Trim()).ToList(),
        EnrollmentMin = enrollmentMin,
        EnrollmentMax = enrollmentMax,
        StartDateFrom = startDateFrom,
        StartDateTo = startDateTo,
        Page = p,
        PageSize = ps
    };

    var total = await repo.CountStudiesByInvestigatorPersonIdAsync(uuid, criteria);
    var studies = await repo.GetStudiesByInvestigatorPersonIdAsync(uuid, criteria);

    // Apply client-side sorting if requested
    if (!string.IsNullOrEmpty(sort))
    {
        var isAscending = !order?.Equals("desc", StringComparison.OrdinalIgnoreCase) ?? true;
        studies = sort.Equals("title", StringComparison.OrdinalIgnoreCase) ? 
            (isAscending ? studies.OrderBy(s => s.BriefTitle).ToList() : studies.OrderByDescending(s => s.BriefTitle).ToList()) :
            sort.Equals("status", StringComparison.OrdinalIgnoreCase) ?
            (isAscending ? studies.OrderBy(s => s.OverallStatus).ToList() : studies.OrderByDescending(s => s.OverallStatus).ToList()) :
            sort.Equals("phase", StringComparison.OrdinalIgnoreCase) ?
            (isAscending ? studies.OrderBy(s => string.Join(",", s.Phases?.Select(p => p.Phase) ?? [])).ToList() : studies.OrderByDescending(s => string.Join(",", s.Phases?.Select(p => p.Phase) ?? [])).ToList()) :
            sort.Equals("enrollment", StringComparison.OrdinalIgnoreCase) ?
            (isAscending ? studies.OrderBy(s => s.EnrollmentCount ?? 0).ToList() : studies.OrderByDescending(s => s.EnrollmentCount ?? 0).ToList()) :
            sort.Equals("startDate", StringComparison.OrdinalIgnoreCase) ?
            (isAscending ? studies.OrderBy(s => s.StartDate.HasValue ? s.StartDate.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue).ToList() : studies.OrderByDescending(s => s.StartDate.HasValue ? s.StartDate.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue).ToList()) :
            studies;
    }

    return Results.Ok(new
    {
        data = studies.Select(s => StudyMapper.ToSummary(s)),
        total,
        page = p,
        pageSize = ps,
        totalPages = (int)Math.Ceiling((double)total / ps)
    });
});

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
        .UseNpgsql(connectionString).Options);
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

app.MapGet("/api/enrichment/breakdown", async () =>
{
    var repo = new StudyRepository(connectionString);
    var breakdown = await repo.GetNpiEnrichmentBreakdownAsync();
    return Results.Ok(breakdown);
});

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

app.MapGet("/api/export/keywords", async (HttpResponse response) =>
{
    using var ctx = new ClinicalTrialsContext(new DbContextOptionsBuilder<ClinicalTrialsContext>()
        .UseNpgsql(connectionString).Options);

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

app.MapGet("/api/word-cloud/conditions", async () =>
{
    var repo = new StudyRepository(connectionString);
    var words = await repo.GetConditionFrequenciesAsync();
    return Results.Ok(words.Select(w => new { w.Text, w.Weight }));
});

app.MapGet("/api/word-cloud/keywords", async () =>
{
    var repo = new StudyRepository(connectionString);
    var words = await repo.GetKeywordFrequenciesAsync();
    return Results.Ok(words.Select(w => new { w.Text, w.Weight }));
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

    // Enrichment coverage stats (NPI)
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

// Event queue endpoints
app.MapGet("/api/event-queue/stats", async () =>
{
    var eventQueueService = new Scrapers.Services.EventQueue.EventQueueService(connectionString);
    var stats = await eventQueueService.GetStatsAsync();
    var byEventType = await eventQueueService.GetEventTypeBreakdownAsync();
    return Results.Ok(new
    {
        stats.PendingCount,
        stats.ProcessingCount,
        stats.CompletedCount,
        stats.DeadLetterCount,
        stats.FailedCount,
        stats.AverageProcessingTimeMs,
        stats.FailureRate,
        stats.EstimatedTimeRemainingMs,
        byEventType = byEventType.Select(et => new
        {
            et.EventType,
            et.Pending,
            et.Processing,
            et.Completed,
            et.Failed,
            et.DeadLetter,
            et.AverageProcessingTimeMs,
            percentiles = et.Percentiles != null ? new
            {
                et.Percentiles.Count,
                et.Percentiles.MinMs,
                et.Percentiles.P50Ms,
                et.Percentiles.P95Ms,
                et.Percentiles.P99Ms,
                et.Percentiles.MaxMs
            } : null
        })
    });
});

app.MapGet("/api/event-queue/duration-history", async (string? eventType, string? period, int? bucketMinutes) =>
{
    var eventQueueService = new Scrapers.Services.EventQueue.EventQueueService(connectionString);
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
    var eventQueueService = new Scrapers.Services.EventQueue.EventQueueService(connectionString);
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
    var eventQueueService = new Scrapers.Services.EventQueue.EventQueueService(connectionString);
    var result = await eventQueueService.RetryEventAsync(eventId);
    return result ? Results.Ok() : Results.NotFound();
});

app.MapPost("/api/event-queue/dead-letter/{eventId:int}/ignore", async (int eventId) =>
{
    var eventQueueService = new Scrapers.Services.EventQueue.EventQueueService(connectionString);
    var result = await eventQueueService.IgnoreEventAsync(eventId);
    return result ? Results.Ok() : Results.NotFound();
});

app.MapGet("/api/data-source-state", async () =>
{
    var dataSourceService = new Scrapers.Services.EventQueue.DataSourceStateService(connectionString);
    var states = await dataSourceService.GetAllStatesAsync();
    return Results.Ok(states.Select(s => new
    {
        s.SourceName,
        s.LastSyncTimestamp,
        s.Status,
        s.ErrorMessage,
        s.UpdatedAt,
        s.RejectedKeywordsTotal
    }));
});

await app.RunAsync();

/// <summary>
/// Parse comma-separated query parameter into list of values.
/// </summary>
static IReadOnlyList<string>? ParseCsvParam(string? param)
{
    if (string.IsNullOrWhiteSpace(param))
        return null;

    return param.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

namespace DataApi
{
    partial class Program { }
}