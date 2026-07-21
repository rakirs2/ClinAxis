using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services.Enrichment;
using Scrapers.Services.EventQueue;

namespace IngestionApp;

internal sealed class InvestigatorEnrichmentService : BackgroundService
{
    private readonly IEventQueueService _eventQueueService;
    private readonly NppesNpiRegistryClient _npiClient;
    private readonly OrcidApiClient _orcidClient;
    private readonly string _connectionString;
    private readonly string _serviceInstanceId;
    private readonly int _pollIntervalSeconds;

    public InvestigatorEnrichmentService(
        IEventQueueService eventQueueService,
        NppesNpiRegistryClient npiClient,
        OrcidApiClient orcidClient,
        string connectionString,
        int pollIntervalSeconds = 30)
    {
        _eventQueueService = eventQueueService ?? throw new ArgumentNullException(nameof(eventQueueService));
        _npiClient = npiClient ?? throw new ArgumentNullException(nameof(npiClient));
        _orcidClient = orcidClient ?? throw new ArgumentNullException(nameof(orcidClient));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
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
                System.Diagnostics.Debug.WriteLine($"InvestigatorEnrichmentService error: {ex}");
                await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessEnrichmentEventAsync(PipelineEventEntity @event, CancellationToken ct)
    {
        if (!Guid.TryParse(@event.Data, out var personId))
            return;

        using var context = new ClinicalTrialsContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(_connectionString).Options);

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

        // Get affiliation from primary affiliation for disambiguation
        var primaryAffil = await context.InvestigatorAffiliations
            .Where(a => a.InvestigatorPersonId == personId && a.IsPrimary)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var affilName = primaryAffil?.InstitutionName;
        var affilState = primaryAffil?.State;

        // --- NPI lookup ---
        var enqueueDiscovered = false;
        if (string.IsNullOrWhiteSpace(person.Npi))
        {
            try
            {
                IReadOnlyList<NpiRegistryResult> npiResults = [];
                foreach (var (tryFirst, tryLast) in nameVariations)
                {
                    npiResults = await _npiClient.SearchByNameAsync(tryFirst, tryLast, affilName, affilState, ct).ConfigureAwait(false);
                    if (npiResults.Count > 0)
                        break;
                }

                foreach (var result in npiResults)
                {
                    var matchedName = $"{result.Basic?.FirstName} {result.Basic?.LastName}".Trim();
                    var matchedOrg = result.Basic?.OrganizationName;
                    var matchedState = result.Addresses is { Count: > 0 } ? result.Addresses[0].State : null;

                    context.PersonIdentifierCandidates.Add(new PersonIdentifierCandidateEntity
                    {
                        PersonId = personId,
                        IdentifierType = "NPI",
                        IdentifierValue = result.Number ?? "",
                        SourceName = "NPPES",
                        MatchedFullName = matchedName,
                        MatchedAffiliation = matchedOrg,
                        MatchedState = matchedState,
                        SourceStatus = result.Status,
                        SourceDeactivatedAt = result.DeactivationDate,
                        IsAutoApproved = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                IReadOnlyList<NpiRegistryResult> resolved;

                // Try affiliation-based filtering (now populated by CT.gov enrichment)
                if (npiResults.Count > 1 && !string.IsNullOrWhiteSpace(affilName))
                {
                    var orgResults = npiResults
                        .Where(r => r.Basic?.OrganizationName != null &&
                            r.Basic.OrganizationName.Contains(affilName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    resolved = orgResults.Count == 1 ? orgResults : npiResults;
                }
                else
                {
                    resolved = npiResults;
                }

                // Try ORCID cross-reference: if person has ORCID, check NPPES identifiers
                if (resolved.Count > 1 && !string.IsNullOrWhiteSpace(person.Orcid))
                {
                    var orcidResults = resolved
                        .Where(r => r.Identifiers != null &&
                            r.Identifiers.Any(id =>
                                string.Equals(id.IdentifierType, "17", StringComparison.Ordinal) &&
                                string.Equals(id.Identifier, person.Orcid, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    if (orcidResults.Count == 1)
                        resolved = orcidResults;
                }

                // State-based tiebreaker: if exactly 1 candidate matches the investigator's state → auto-approve
                if (resolved.Count > 1 && !string.IsNullOrWhiteSpace(affilState))
                {
                    var stateResults = resolved
                        .Where(r => r.Addresses is { Count: > 0 } &&
                            r.Addresses.Any(a =>
                                string.Equals(a.State, affilState, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    if (stateResults.Count == 1)
                        resolved = stateResults;
                }

                if (resolved.Count == 1 && resolved[0].Status != "D")
                {
                    person.Npi = resolved[0].Number;
                    person.NpiEnrichmentResult = "assigned";
                    var candidate = await context.PersonIdentifierCandidates
                        .Where(c => c.PersonId == personId && c.IdentifierType == "NPI")
                        .FirstOrDefaultAsync(ct).ConfigureAwait(false);
                    if (candidate != null)
                        candidate.IsAutoApproved = true;
                    enqueueDiscovered = true;
                }
                else if (npiResults.Count == 0)
                {
                    person.NpiEnrichmentResult = "not_found";
                }
                else
                {
                    person.NpiEnrichmentResult = "ambiguous";
                }
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"NPPES lookup failed for {person.FullName}: {ex.Message}");
                person.NpiEnrichmentResult = "error";
            }
        }

        person.NpiLookupAttemptedAt = DateTime.UtcNow;
        person.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        if (enqueueDiscovered)
        {
            await _eventQueueService.EnqueueAsync("investigator.discovered", personId.ToString(), ct).ConfigureAwait(false);
            await _eventQueueService.EnqueueAsync("medicare.utilization", personId.ToString(), ct).ConfigureAwait(false);
            await _eventQueueService.EnqueueAsync("cms.openpayments", personId.ToString(), ct).ConfigureAwait(false);
        }
    }
}
