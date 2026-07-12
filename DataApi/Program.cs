using DataApi;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5003");

var connectionString = ConnectionStringProvider.Default;

// Retry database connection during startup to handle transient DB delays
var startupRepo = new StudyRepository(connectionString);
var maxRetries = 5;
var retryDelay = TimeSpan.FromSeconds(3);
for (int attempt = 1; attempt <= maxRetries; attempt++)
{
    try
    {
        await startupRepo.EnsureSchemaAsync();
        break;
    }
    catch (Exception ex) when (attempt < maxRetries)
    {
        await Console.Error.WriteLineAsync($"Database connection failed (attempt {attempt}/{maxRetries}): {ex.Message}");
        await Task.Delay(retryDelay);
    }
}

// Seed database with test data only in non-Production environments
if (!builder.Environment.IsProduction())
{
    var seeder = new DatabaseSeeder(connectionString);
    await seeder.SeedIfEmptyAsync();
}

builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

app.MapHealthChecks("/health");

// Endpoints for advanced search filter options
app.MapGet("/api/distinct-conditions", async () =>
{
    var repo = new StudyRepository(connectionString);
    var conditions = await repo.GetDistinctConditionsAsync();
    return Results.Ok(conditions);
});

app.MapGet("/api/distinct-locations", async (string? country, string? state, string? city) =>
{
    var repo = new StudyRepository(connectionString);
    var (countries, states, cities, facilities) = await repo.GetDistinctLocationsAsync(country, state, city);
    return Results.Ok(new
    {
        countries,
        states,
        cities,
        facilities
    });
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
    var ps = Math.Clamp(pageSize ?? 20, 1, 100);

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

app.MapGet("/api/investigators", async (int? page, int? pageSize, string? search) =>
{
    var repo = new StudyRepository(connectionString);
    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 20, 1, 100);

    IReadOnlyList<InvestigatorSummary> investigators = await repo.GetInvestigatorsPagedAsync(p, ps, search);
    var total = await repo.CountInvestigatorsFilteredAsync(search);

    return Results.Ok(new
    {
        data = investigators,
        total,
        page = p,
        pageSize = ps,
        totalPages = (int)Math.Ceiling((double)total / ps)
    });
});

// Investigator detail endpoint
app.MapGet("/api/investigators/{uuid}", async (Guid uuid) =>
{
    var repo = new StudyRepository(connectionString);
    var investigator = await repo.GetInvestigatorByUuidAsync(uuid);
    
    if (investigator == null)
    {
        return Results.NotFound(new { message = "Investigator not found" });
    }

    // Get all studies for this investigator using a basic criteria (no filters)
    var criteria = new StudySearchCriteria
    {
        Page = 1,
        PageSize = int.MaxValue // Get all studies for this investigator
    };
    var studies = await repo.GetStudiesByInvestigatorUuidAsync(uuid, criteria);

    var detail = InvestigatorMapper.ToDetail(investigator, studies);
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
    var investigator = await repo.GetInvestigatorByUuidAsync(uuid);
    
    if (investigator == null)
    {
        return Results.NotFound(new { message = "Investigator not found" });
    }

    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 20, 1, 100);

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

    var studies = await repo.GetStudiesByInvestigatorUuidAsync(uuid, criteria);

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
        total = studies.Count,
        page = p,
        pageSize = ps,
        totalPages = (int)Math.Ceiling((double)studies.Count / ps)
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

app.MapGet("/api/stats", async () =>
{
    var repo = new StudyRepository(connectionString);
    var studies = await repo.CountStudiesAsync();
    var investigators = await repo.CountInvestigatorsAsync();
    var pubmedPapers = await repo.CountPubmedStudiesAsync();
    var keywords = await repo.CountKeywordsAsync();
    var authors = await repo.CountAuthorsAsync();
    return Results.Ok(new
    {
        totalStudies = studies,
        totalInvestigators = investigators,
        totalPubmedPapers = pubmedPapers,
        totalKeywords = keywords,
        totalAuthors = authors
    });
});

app.MapGet("/api/telemetry", async () =>
{
    var repo = new StudyRepository(connectionString);

    var studies = await repo.CountStudiesAsync();
    var investigators = await repo.CountInvestigatorsAsync();
    var pubmedPapers = await repo.CountPubmedStudiesAsync();
    var keywords = await repo.CountKeywordsAsync();
    var authors = await repo.CountAuthorsAsync();

    List<PipelineRunEntity> recentRuns = await repo.GetPipelineRunsAsync(1, 5);
    IReadOnlyList<ScrapeEventEntity> recentEvents = await repo.GetRecentScrapeEventsAsync(20);

    var piCount = await repo.CountPiAggregationsAsync();
    IReadOnlyList<CategoryTypeCount> categoryByType = await repo.CountCategoryAggregationsByTypeAsync();

    return Results.Ok(new
    {
        db = new
        {
            totalStudies = studies,
            totalInvestigators = investigators,
            totalPubmedPapers = pubmedPapers,
            totalKeywords = keywords,
            totalAuthors = authors
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
        aggregations = new
        {
            piAggregationCount = piCount,
            categoryAggregationCount = categoryByType.Sum(c => c.Count)
        }
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
    return Results.Ok(stats);
});

app.MapGet("/api/event-queue/dead-letter", async () =>
{
    var eventQueueService = new Scrapers.Services.EventQueue.EventQueueService(connectionString);
    var deadLetterEvents = await eventQueueService.GetDeadLetterEventsAsync(100);
    return Results.Ok(deadLetterEvents.Select(e => new
    {
        e.Id,
        e.EventType,
        e.Data,
        e.Status,
        e.ErrorMessage,
        e.RetryCount,
        e.CreatedAt,
        e.CompletedAt
    }));
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
        s.UpdatedAt
    }));
});

app.MapPost("/api/event-queue/dead-letter/{id}/retry", async (int id) =>
{
    var eventQueueService = new Scrapers.Services.EventQueue.EventQueueService(connectionString);
    await eventQueueService.RetryDeadLetterEventAsync(id);
    return Results.Ok(new { message = "Event moved back to pending queue for retry" });
});

app.MapPost("/api/event-queue/dead-letter/{id}/ignore", async (int id) =>
{
    var eventQueueService = new Scrapers.Services.EventQueue.EventQueueService(connectionString);
    await eventQueueService.IgnoreDeadLetterEventAsync(id);
    return Results.Ok(new { message = "Event marked as ignored" });
});

// CMS Provider endpoints
app.MapGet("/api/providers", async (int? page, int? pageSize, string? search, string? specialty, string? state) =>
{
    var repo = new StudyRepository(connectionString);
    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 20, 1, 100);

    var providers = await repo.SearchCmsProvidersAsync(p, ps, search, specialty, state);
    var total = await repo.CountCmsProvidersFilteredAsync(search, specialty, state);

    return Results.Ok(new
    {
        data = providers,
        total,
        page = p,
        pageSize = ps,
        totalPages = (int)Math.Ceiling((double)total / ps)
    });
});

app.MapGet("/api/providers/{npi}", async (string npi) =>
{
    var repo = new StudyRepository(connectionString);
    var provider = await repo.GetCmsProviderByNpiAsync(npi);

    if (provider is null)
        return Results.NotFound(new { error = "Provider not found" });

    return Results.Ok(new
    {
        provider.Id,
        provider.Uuid,
        provider.Npi,
        provider.ProviderName,
        provider.Gender,
        provider.Credential,
        provider.MedicalSchoolName,
        provider.GraduationYear,
        provider.PrimarySpecialty,
        provider.SecondarySpecialty,
        provider.OrganizationLegalName,
        provider.PracticeAddressCity,
        provider.PracticeAddressState,
        provider.PracticeAddressZip,
        provider.MedicareParticipation,
        provider.TotalMedicareServices,
        provider.TotalMedicarePayments,
        provider.TotalMedicareBeneficiaries
    });
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