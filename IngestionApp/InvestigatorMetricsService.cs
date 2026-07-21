using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services.Enrichment;
using Scrapers.Services.EventQueue;

namespace IngestionApp;

/// <summary>
/// Background service that enriches investigators with citation metrics from Semantic Scholar.
/// Implements both event-driven and self-healing patterns:
/// - Event-driven: processes "investigator.metrics" events enqueued after publication scrubbing
/// - Self-healing: scans for investigators with papers but no metrics lookup attempted
/// </summary>
internal sealed class InvestigatorMetricsService : BackgroundService
{
    private readonly IEventQueueService _eventQueueService;
    private readonly SemanticScholarClient _semanticScholarClient;
    private readonly string _connectionString;
    private readonly string _serviceInstanceId;
    private readonly int _pollIntervalSeconds;

    public InvestigatorMetricsService(
        IEventQueueService eventQueueService,
        SemanticScholarClient semanticScholarClient,
        string connectionString,
        int pollIntervalSeconds = 30)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        _semanticScholarClient = semanticScholarClient ?? throw new ArgumentNullException(nameof(semanticScholarClient));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _pollIntervalSeconds = pollIntervalSeconds;
        _serviceInstanceId = $"{System.Environment.MachineName}-metrics-{System.Environment.ProcessId}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Release stuck events that have been claimed for too long
                await _eventQueueService.ReleaseStuckEventsAsync(TimeSpan.FromMinutes(30), stoppingToken).ConfigureAwait(false);

                // Try to claim and process an event
                var @event = await _eventQueueService.ClaimNextPendingEventAsync(
                    _serviceInstanceId,
                    eventTypes: ["investigator.metrics"],
                    stoppingToken).ConfigureAwait(false);

                if (@event == null)
                {
                    // No pending events; run self-healing scan for metrics that were never attempted
                    await ProcessManualMetricsPersonsAsync(stoppingToken).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    await ProcessMetricsEventAsync(@event, stoppingToken).ConfigureAwait(false);
                    await _eventQueueService.CompleteEventAsync(@event.Id, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await _eventQueueService.FailEventAsync(
                        @event.Id,
                        $"{ex.GetType().Name}: {ex.Message}",
                        stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InvestigatorMetricsService error: {ex}");
                await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Self-healing scan: finds investigators who have papers but no Semantic Scholar metrics lookup.
    /// Catches investigators whose metrics event was lost, failed, or never enqueued.
    /// </summary>
    private async Task ProcessManualMetricsPersonsAsync(CancellationToken ct)
    {
        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString).Options);

        // Find persons who have papers but no SemanticScholar metrics attempt
        var personsNeedingMetrics = await context.InvestigatorPersons
            .Include(p => p.InvestigatorPapers)
            .Include(p => p.Metrics)
            .Where(p => p.InvestigatorPapers!.Any()
                     && !p.Metrics!.Any(m => m.Source == "SemanticScholar"))
            .Take(10)
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var person in personsNeedingMetrics)
        {
            try
            {
                await FetchAndStoreMetricsAsync(context, person, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Metrics lookup failed for {person.FullName}: {ex.Message}");
                // Store error but don't crash the loop
                var existingMetric = context.InvestigatorMetrics.FirstOrDefault(m => m.InvestigatorPersonId == person.Id && m.Source == "SemanticScholar");
                if (existingMetric != null)
                {
                    existingMetric.LookupResult = "error";
                    existingMetric.LookupErrorMessage = ex.Message;
                    existingMetric.LookupAttemptedAt = DateTime.UtcNow;
                    existingMetric.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        if (personsNeedingMetrics.Count > 0)
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Process a single investigator metrics event.
    /// </summary>
    private async Task ProcessMetricsEventAsync(PipelineEventEntity @event, CancellationToken ct)
    {
        if (!Guid.TryParse(@event.Data, out var personId))
            throw new InvalidOperationException($"Invalid event data: '{@event.Data}' is not a valid GUID.");

        using var context = new ClinicalTrialsContext(
            new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString).Options);

        var person = await context.InvestigatorPersons
            .Include(p => p.Metrics)
            .FirstOrDefaultAsync(p => p.Id == personId, ct).ConfigureAwait(false);

        if (person == null)
            throw new InvalidOperationException($"InvestigatorPerson not found for id={personId}.");

        await FetchAndStoreMetricsAsync(context, person, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetch metrics from Semantic Scholar and store in database.
    /// </summary>
    private async Task FetchAndStoreMetricsAsync(ClinicalTrialsContext context, InvestigatorPersonEntity person, CancellationToken ct)
    {
        // Check if we've already attempted SemanticScholar lookup for this person
        var existingMetric = context.InvestigatorMetrics.FirstOrDefault(m => m.InvestigatorPersonId == person.Id && m.Source == "SemanticScholar");

        if (existingMetric?.LookupAttemptedAt != null)
            return; // Already attempted; don't re-query

        // Query Semantic Scholar API
        var author = await _semanticScholarClient.SearchByNameAsync(person.FullName, ct: ct).ConfigureAwait(false);

        // Create or update metric record
        if (existingMetric == null)
        {
            existingMetric = new InvestigatorMetricEntity
            {
                InvestigatorPersonId = person.Id,
                Source = "SemanticScholar",
                CreatedAt = DateTime.UtcNow
            };
            context.InvestigatorMetrics.Add(existingMetric);
        }

        if (author != null)
        {
            existingMetric.HIndex = author.HIndex;
            existingMetric.CitationCount = author.CitationCount;
            existingMetric.I10Index = author.I10Index;
            existingMetric.TotalPapers = author.PaperCount;
            existingMetric.ExternalAuthorId = author.AuthorId;
            existingMetric.LookupResult = "found";
        }
        else
        {
            existingMetric.LookupResult = "not_found";
        }

        existingMetric.LookupAttemptedAt = DateTime.UtcNow;
        existingMetric.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
