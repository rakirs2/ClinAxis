using System.Text.Json;
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

        // Parse name for API queries
        var nameParts = person.FullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 1 ? nameParts[0] : "";
        var lastName = nameParts.Length > 1 ? nameParts[1] : nameParts[0];

        // Get affiliation from primary affiliation for disambiguation
        var primaryAffil = await context.InvestigatorAffiliations
            .Where(a => a.InvestigatorPersonId == personId && a.IsPrimary)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var affilName = primaryAffil?.InstitutionName;
        var affilState = primaryAffil?.State;

        // --- NPI lookup ---
        if (string.IsNullOrWhiteSpace(person.Npi))
        {
            try
            {
                var npiResults = await _npiClient.SearchByNameAsync(firstName, lastName, affilName, affilState, ct).ConfigureAwait(false);

                foreach (var result in npiResults)
                {
                    var candidate = new PersonIdentifierCandidateEntity
                    {
                        PersonId = personId,
                        IdentifierType = "NPI",
                        IdentifierValue = result.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        SourceName = "NPPES",
                        MatchedFullName = $"{result.Basic?.FirstName} {result.Basic?.LastName}".Trim(),
                        MatchedAffiliation = result.Basic?.OrganizationName,
                        MatchedState = result.Addresses is { Count: > 0 } ? result.Addresses[0].State : null,
                        SourceStatus = result.Status,
                        SourceDeactivatedAt = result.DeactivationDate,
                        IsAutoApproved = false, // will set below if exact match
                        CreatedAt = DateTime.UtcNow
                    };

                    context.PersonIdentifierCandidates.Add(candidate);
                }

                // Auto-assign only when exactly 1 result and it's active
                if (npiResults.Count == 1 && npiResults[0].Status != "D")
                {
                    person.Npi = npiResults[0].Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    // Update the candidate to mark it auto-approved
                    var candidate = await context.PersonIdentifierCandidates
                        .Where(c => c.PersonId == personId && c.IdentifierType == "NPI")
                        .FirstOrDefaultAsync(ct).ConfigureAwait(false);
                    if (candidate != null)
                        candidate.IsAutoApproved = true;
                }
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"NPPES lookup failed for {person.FullName}: {ex.Message}");
            }
        }

        // --- ORCID lookup (skip if already have one) ---
        if (string.IsNullOrWhiteSpace(person.Orcid))
        {
            try
            {
                var orcidResults = await _orcidClient.SearchByNameAsync(firstName, lastName, ct).ConfigureAwait(false);

                foreach (var result in orcidResults)
                {
                    context.PersonIdentifierCandidates.Add(new PersonIdentifierCandidateEntity
                    {
                        PersonId = personId,
                        IdentifierType = "ORCID",
                        IdentifierValue = result.Path ?? "",
                        SourceName = "ORCID",
                        MatchedFullName = $"{firstName} {lastName}",
                        IsAutoApproved = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (orcidResults.Count == 1 && orcidResults[0].Path != null)
                {
                    person.Orcid = orcidResults[0].Path;
                    var candidate = await context.PersonIdentifierCandidates
                        .Where(c => c.PersonId == personId && c.IdentifierType == "ORCID")
                        .FirstOrDefaultAsync(ct).ConfigureAwait(false);
                    if (candidate != null)
                        candidate.IsAutoApproved = true;
                }
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"ORCID lookup failed for {person.FullName}: {ex.Message}");
            }
        }

        person.NpiLookupAttemptedAt = DateTime.UtcNow;
        person.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
