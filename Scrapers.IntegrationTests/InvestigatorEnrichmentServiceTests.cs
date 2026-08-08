using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using IngestionApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;
using Scrapers.Services.Enrichment;
using Scrapers.Services.EventQueue;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class InvestigatorEnrichmentServiceTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Enrichment_SetsIsAutoApproved_OnAssignedCandidate()
    {
        var person = new InvestigatorPersonEntity
        {
            FullName = "John Smith",
            IsHuman = true
        };
        Context.InvestigatorPersons.Add(person);
        Context.InvestigatorAffiliations.Add(new InvestigatorAffiliationEntity
        {
            InvestigatorPersonId = person.Id,
            InstitutionName = "Mayo Clinic",
            City = "Rochester",
            State = "MN",
            IsPrimary = true
        });
        await Context.SaveChangesAsync();

        var fixture = await File.ReadAllTextAsync("Data/NppesNpi/search-multiple-results.json");
        var handler = new FakeNppesHandler(fixture);
        var queue = new RecordingEventQueue();
        var service = new InvestigatorEnrichmentService(
            queue,
            new NppesNpiRegistryClient(new HttpClient(handler)),
            new OrcidApiClient(new HttpClient(new FakeNppesHandler("{}"))),
            modelService: null,
            ConnectionString,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InvestigatorEnrichmentService>.Instance);

        await service.ProcessEnrichmentEventAsync(
            new PipelineEventEntity { Data = person.Id.ToString() },
            CancellationToken.None);

        var savedPerson = Context.InvestigatorPersons.AsNoTracking().Single(p => p.Id == person.Id);
        Assert.AreEqual("assigned", savedPerson.NpiEnrichmentResult,
            $"result={savedPerson.NpiEnrichmentResult} attempted={savedPerson.NpiLookupAttemptedAt}");
        Assert.AreEqual("1234567890", savedPerson.Npi);

        var allCandidates = Context.PersonIdentifierCandidates.AsNoTracking()
            .Where(c => c.PersonId == person.Id)
            .OrderBy(c => c.IdentifierValue)
            .ToList();
        Assert.AreEqual(2, allCandidates.Count);

        var winner = allCandidates.Single(c => c.IdentifierValue == "1234567890");
        Assert.IsTrue(winner.IsAutoApproved);
        Assert.AreEqual(1.0, winner.RuleScore);

        var loser = allCandidates.Single(c => c.IdentifierValue == "9876543210");
        Assert.IsFalse(loser.IsAutoApproved);
        // 40/65: exact name matched; state (OH vs MN), org, city mismatch.
        Assert.AreEqual(40.0 / 65.0, loser.RuleScore);

        CollectionAssert.AreEqual(
            new[] { "investigator.discovered", "medicare.utilization", "cms.openpayments" },
            queue.Enqueued.Select(e => e.Type).ToArray());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Enrichment_DuplicatePersonNpiCollision_CompletesWithoutDeadLettering()
    {
        // Two person rows for the same real physician ("John Smith" vs "John A Smith"):
        // the person lookup dedups by exact full name only, so both rows get their own
        // enrichment event and both match the same NPPES record. The second NPI
        // assignment would violate the unique npi index — the service must record the
        // attempt and complete instead of dead-lettering.
        var first = new InvestigatorPersonEntity { FullName = "John Smith", IsHuman = true };
        var duplicate = new InvestigatorPersonEntity { FullName = "John A Smith", IsHuman = true };
        Context.InvestigatorPersons.AddRange(first, duplicate);
        Context.InvestigatorAffiliations.AddRange(
            new InvestigatorAffiliationEntity
            {
                InvestigatorPersonId = first.Id,
                InstitutionName = "Mayo Clinic",
                City = "Rochester",
                State = "MN",
                IsPrimary = true
            },
            new InvestigatorAffiliationEntity
            {
                InvestigatorPersonId = duplicate.Id,
                InstitutionName = "Mayo Clinic",
                City = "Rochester",
                State = "MN",
                IsPrimary = true
            });
        await Context.SaveChangesAsync();

        var fixture = await File.ReadAllTextAsync("Data/NppesNpi/search-multiple-results.json");
        var handler = new FakeNppesHandler(fixture);
        var queue = new RecordingEventQueue();
        var service = new InvestigatorEnrichmentService(
            queue,
            new NppesNpiRegistryClient(new HttpClient(handler)),
            new OrcidApiClient(new HttpClient(new FakeNppesHandler("{}"))),
            modelService: null,
            ConnectionString,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InvestigatorEnrichmentService>.Instance);

        await service.ProcessEnrichmentEventAsync(
            new PipelineEventEntity { Data = first.Id.ToString() },
            CancellationToken.None);

        // Before the collision fix this threw DbUpdateException (23505 on the npi
        // index), which dead-lettered the event after 3 retries.
        await service.ProcessEnrichmentEventAsync(
            new PipelineEventEntity { Data = duplicate.Id.ToString() },
            CancellationToken.None);

        var savedFirst = Context.InvestigatorPersons.AsNoTracking().Single(p => p.Id == first.Id);
        var savedDuplicate = Context.InvestigatorPersons.AsNoTracking().Single(p => p.Id == duplicate.Id);

        Assert.AreEqual("1234567890", savedFirst.Npi);
        Assert.AreEqual("assigned", savedFirst.NpiEnrichmentResult);
        Assert.IsNull(savedDuplicate.Npi);
        Assert.AreEqual("duplicate", savedDuplicate.NpiEnrichmentResult);
        Assert.IsNotNull(savedDuplicate.NpiLookupAttemptedAt);

        // Downstream events were enqueued only for the canonical row (3 = first run).
        Assert.AreEqual(3, queue.Enqueued.Count);
        // NPPES data for the duplicate row is preserved, not discarded.
        Assert.AreEqual(2, Context.PersonIdentifierCandidates.AsNoTracking().Count(c => c.PersonId == duplicate.Id));
    }

    private sealed class FakeNppesHandler : HttpMessageHandler
    {
        private readonly string _json;

        public FakeNppesHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        }
    }

    private sealed class RecordingEventQueue : IEventQueueService
    {
        public List<(string Type, string? Data)> Enqueued { get; } = [];

        public Task EnqueueAsync(string eventType, string? data = null, CancellationToken ct = default)
        {
            Enqueued.Add((eventType, data));
            return Task.CompletedTask;
        }

        public Task<PipelineEventEntity?> ClaimNextPendingEventAsync(
            string claimedBy, string[]? eventTypes = null, CancellationToken ct = default)
            => Task.FromResult<PipelineEventEntity?>(null);

        public Task<bool> HasActiveEventAsync(string eventType, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<bool> HasUnresolvedDiscoveryEventAsync(DateTime? lastUpdatedPost, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task CompleteEventAsync(int eventId, CancellationToken ct = default) => Task.CompletedTask;

        public Task FailEventAsync(int eventId, string errorMessage, CancellationToken ct = default) => Task.CompletedTask;

        public Task<List<PipelineEventEntity>> GetDeadLetterEventsAsync(int limit = 100, CancellationToken ct = default)
            => Task.FromResult(new List<PipelineEventEntity>());

        public Task<EventQueueStats> GetStatsAsync(CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task RetryDeadLetterEventAsync(int eventId, CancellationToken ct = default) => Task.CompletedTask;

    public Task<int> RetryAllDeadLetterEventsAsync(string? eventType = null, CancellationToken ct = default) => Task.FromResult(0);

        public Task IgnoreDeadLetterEventAsync(int eventId, CancellationToken ct = default) => Task.CompletedTask;

        public Task ReleaseEventAsync(int eventId, CancellationToken ct = default) => Task.CompletedTask;

        public Task ReleaseStuckEventsAsync(TimeSpan claimTimeout, CancellationToken ct = default) => Task.CompletedTask;

        public Task<List<EventTypeBreakdown>> GetEventTypeBreakdownAsync(CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<List<DurationHistoryPoint>> GetDurationHistoryAsync(
            string? eventType = null, string period = "24h", int bucketMinutes = 60, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
