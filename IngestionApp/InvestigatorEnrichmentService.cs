using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services.Enrichment;
using Scrapers.Services.EventQueue;
using Scrapers.Utilities;

namespace IngestionApp;

internal sealed class InvestigatorEnrichmentService : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogLoopNetworkError =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1, "EnrichmentNetworkError"),
            "Investigator enrichment loop failed with a network error");

    private static readonly Action<ILogger, Exception?> LogLoopError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, "EnrichmentLoopError"),
            "Investigator enrichment loop failed");

    private static readonly Action<ILogger, string, Exception> LogNppesLookupFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, "NppesLookupFailed"),
            "NPPES lookup failed for {PersonName}");

    private readonly IEventQueueService _eventQueueService;
    private readonly NppesNpiRegistryClient _npiClient;
    private readonly OrcidApiClient _orcidClient;
    private readonly NpiModelService? _modelService;
    private readonly string _connectionString;
    private readonly string _serviceInstanceId;
    private readonly int _pollIntervalSeconds;
    private readonly ILogger<InvestigatorEnrichmentService> _logger;

    public InvestigatorEnrichmentService(
        IEventQueueService eventQueueService,
        NppesNpiRegistryClient npiClient,
        OrcidApiClient orcidClient,
        NpiModelService? modelService,
        string connectionString,
        ILogger<InvestigatorEnrichmentService> logger,
        int pollIntervalSeconds = 30)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        _npiClient = npiClient ?? throw new ArgumentNullException(nameof(npiClient));
        _orcidClient = orcidClient ?? throw new ArgumentNullException(nameof(orcidClient));
        _modelService = modelService;
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pollIntervalSeconds = pollIntervalSeconds;
        _serviceInstanceId = $"{System.Environment.MachineName}-enrichment-{System.Environment.ProcessId}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _eventQueueService.ReleaseStuckEventsAsync(TimeSpan.FromMinutes(30), stoppingToken).ConfigureAwait(false);

                var @event = await _eventQueueService.ClaimNextPendingEventAsync(
                    _serviceInstanceId,
                    eventTypes: ["investigator.enrichment"],
                    stoppingToken).ConfigureAwait(false);

                if (@event == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    await ProcessEnrichmentEventAsync(@event, stoppingToken).ConfigureAwait(false);
                    await _eventQueueService.CompleteEventAsync(@event.Id, stoppingToken).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    await _eventQueueService.FailEventAsync(
                        @event.Id,
                        ex.ToString(),
                        stoppingToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    await _eventQueueService.FailEventAsync(
                        @event.Id,
                        ex.ToString(),
                        stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await _eventQueueService.FailEventAsync(
                        @event.Id,
                        ex.ToString(),
                        stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpRequestException ex)
            {
                LogLoopNetworkError(_logger, ex);
                await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogLoopError(_logger, ex);
                await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    internal async Task ProcessEnrichmentEventAsync(PipelineEventEntity @event, CancellationToken ct)
    {
        if (!Guid.TryParse(@event.Data, out var personId))
            return;

        using var context = new ClinicalTrialsContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ClinicalTrialsContext>()
                .ConfigureNpgsql(_connectionString).Options);

        var person = await context.InvestigatorPersons
            .FirstOrDefaultAsync(p => p.Id == personId, ct).ConfigureAwait(false);

        if (person == null)
            return;

        // Don't re-query if we already attempted NPI lookup
        if (person.NpiLookupAttemptedAt != null)
            return;

        // Parse name for API queries — try multiple formats for better matching
        var nameParts = person.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 1 ? nameParts[0] : "";
        var lastName = nameParts.Length > 1 ? nameParts[^1] : nameParts[0];

        // Generate name variations: "John A Smith" → try "John"+"Smith", then "John"+"A Smith"
        var nameVariations = new List<(string First, string Last)>
        {
            (firstName, lastName)
        };

        if (nameParts.Length > 2)
        {
            nameVariations.Add((nameParts[0], string.Join(" ", nameParts[1..])));
            if (nameParts.Length == 3 && nameParts[1].Length <= 2)
            {
                nameVariations.Add((nameParts[0], nameParts[^1]));
            }
        }

        // Build the person signal profile: primary affiliation + specialty derived from
        // the MeSH descriptors of the person's studies (used for NPI candidate scoring).
        var primaryAffil = await context.InvestigatorAffiliations
            .Where(a => a.InvestigatorPersonId == personId && a.IsPrimary)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        var meshDescriptorNames = await context.StudyInvestigators
            .Where(si => si.InvestigatorPersonId == personId && si.Study != null)
            .SelectMany(si => si.Study!.Conditions!)
            .Where(c => c.MeshDescriptor != null)
            .Select(c => c.MeshDescriptor!.Name)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        var profile = NpiFeatureExtractor.BuildProfile(
            person.FullName,
            person.Orcid,
            primaryAffil?.InstitutionName,
            primaryAffil?.Department,
            primaryAffil?.City,
            primaryAffil?.State,
            meshDescriptorNames);

        // --- NPI lookup ---
        var enqueueDiscovered = false;
        if (string.IsNullOrWhiteSpace(person.Npi))
        {
            try
            {
                IReadOnlyList<NpiRegistryResult> npiResults = [];
                foreach (var (tryFirst, tryLast) in nameVariations)
                {
                    npiResults = await _npiClient.SearchByNameAsync(tryFirst, tryLast, ct).ConfigureAwait(false);
                    if (npiResults.Count > 0)
                        break;
                }

                var entities = new List<(PersonIdentifierCandidateEntity Entity, NpiCandidateFeatures Features)>();
                foreach (var result in npiResults)
                {
                    var features = NpiFeatureExtractor.Extract(profile, result);

                    var entity = new PersonIdentifierCandidateEntity
                    {
                        PersonId = personId,
                        IdentifierType = "NPI",
                        IdentifierValue = features.Number,
                        SourceName = "NPPES",
                        MatchedFullName = features.MatchedFullName,
                        MatchedAffiliation = features.MatchedAffiliation,
                        MatchedState = features.MatchedState,
                        MatchedCity = features.MatchedCity,
                        MatchedMiddleName = features.MatchedMiddleName,
                        MatchedCredential = features.MatchedCredential,
                        MatchedNamePrefix = features.MatchedNamePrefix,
                        MatchedGender = features.MatchedGender,
                        MatchedTaxonomyDesc = features.MatchedTaxonomyDesc,
                        MatchedTaxonomyState = features.MatchedTaxonomyState,
                        MatchedTaxonomyLicense = features.MatchedTaxonomyLicense,
                        MatchedOtherNamesJson = features.MatchedOtherNamesJson,
                        MatchedIdentifiersJson = features.MatchedIdentifiersJson,
                        SourceStatus = result.Status,
                        SourceDeactivatedAt = result.DeactivationDate,
                        IsAutoApproved = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.PersonIdentifierCandidates.Add(entity);
                    entities.Add((entity, features));
                }

                var resolution = NpiCandidateScorer.Resolve(entities.Select(e => e.Features).ToList());

                foreach (var (entity, features) in entities)
                {
                    entity.RuleScore = features.Score;
                    if (_modelService != null)
                    {
                        // Parallel A/B recording: the model never changes the
                        // decision — the rule scorer stays authoritative.
                        entity.ModelScore = _modelService.Predict(features);
                    }
                }

                switch (resolution.Outcome)
                {
                    case NpiCandidateScorer.NpiResolutionOutcome.Assigned:
                        person.Npi = resolution.AssignedNumber;
                        person.NpiEnrichmentResult = "assigned";
                        // The candidates are still only in the change tracker (SaveChanges
                        // runs after the switch), so a database query cannot see them.
                        // Mark the winner in memory instead.
                        var winner = entities
                            .FirstOrDefault(e => e.Features.Number == resolution.AssignedNumber)
                            .Entity;
                        if (winner != null)
                            winner.IsAutoApproved = true;
                        enqueueDiscovered = true;
                        break;
                    case NpiCandidateScorer.NpiResolutionOutcome.NotFound:
                        person.NpiEnrichmentResult = "not_found";
                        break;
                    default:
                        person.NpiEnrichmentResult = "ambiguous";
                        break;
                }
            }
            catch (HttpRequestException ex)
            {
                LogNppesLookupFailed(_logger, person.FullName, ex);
                person.NpiEnrichmentResult = "error";
            }
        }

        person.NpiLookupAttemptedAt = DateTime.UtcNow;
        person.UpdatedAt = DateTime.UtcNow;

        try
        {
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (NpiCollisionDetector.IsNpiUniqueViolation(ex))
        {
            // Duplicate person row ("John Smith" vs "John A Smith"): the NPI is
            // already assigned to the canonical row. Record the attempt without
            // the NPI so the event completes instead of dead-lettering, and skip
            // downstream events — they run against the row that owns the NPI.
            person.Npi = null;
            person.NpiEnrichmentResult = "duplicate";
            enqueueDiscovered = false;
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        if (enqueueDiscovered)
        {
            await _eventQueueService.EnqueueAsync("investigator.discovered", personId.ToString(), ct).ConfigureAwait(false);
            await _eventQueueService.EnqueueAsync("medicare.utilization", personId.ToString(), ct).ConfigureAwait(false);
            await _eventQueueService.EnqueueAsync("cms.openpayments", personId.ToString(), ct).ConfigureAwait(false);
        }
    }
}
