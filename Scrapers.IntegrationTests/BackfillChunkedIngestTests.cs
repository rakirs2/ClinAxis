using System.Net;
using System.Net.Http.Headers;
using IngestionApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Services;
using Scrapers.Services.EventQueue;
using Scrapers.Testing;
using Scrapers.Utilities;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class BackfillChunkedIngestTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task BackfillChunkEvent_IngestsItsWindow_AndSurvivesShortClaimTimeout()
    {
        var queue = new EventQueueService(ConnectionString);

        // 1. Enqueue a backfill chunk event with an inclusive date window.
        const string payload = """{"chunkIndex":0,"count":4,"dateFrom":"2026-07-01","dateTo":"2026-08-01","sweepStartedUtc":"2026-08-03T12:00:00Z"}""";
        await queue.EnqueueAsync("studies.backfill", payload);

        // 2. Claim it; the payload must round-trip through the parser the worker uses.
        var @event = await queue.ClaimNextPendingEventAsync("test-worker", eventTypes: ["studies.backfill"]);
        Assert.IsNotNull(@event);
        Assert.AreEqual("processing", @event!.Status);
        Assert.IsTrue(BackfillEventPayload.TryParse(@event.Data, out var parsed));
        Assert.IsNotNull(parsed);
        Assert.AreEqual(new DateOnly(2026, 7, 1), parsed.DateFrom);
        Assert.AreEqual(new DateOnly(2026, 8, 1), parsed.DateTo);

        // 3. Per-type claim timeout: a 30-min release must NOT release a backfill claim.
        @event.ClaimedAt = DateTime.UtcNow.AddMinutes(-45);
        Context.PipelineEvents.Update(@event);
        await Context.SaveChangesAsync();

        await queue.ReleaseStuckEventsAsync(TimeSpan.FromMinutes(30));

        var stillProcessing = await Context.PipelineEvents.AsNoTracking().SingleAsync(e => e.Id == @event.Id);
        Assert.AreEqual("processing", stillProcessing.Status,
            "Backfill claims must survive the short claim timeout used by the other services.");

        // Ordinary claims use the short timeout independently of backfill claims.
        var ordinaryWindowStart = new DateTime(2026, 8, 6, 20, 0, 0, DateTimeKind.Utc);
        var ordinaryPayload = new IncrementalDiscoveryEventPayload(
            1,
            ordinaryWindowStart,
            ordinaryWindowStart.AddHours(1)).ToJson();
        await queue.EnqueueAsync("studies.discovered", ordinaryPayload);
        var ordinaryEvent = await queue.ClaimNextPendingEventAsync("ordinary-worker", eventTypes: ["studies.discovered"]);
        Assert.IsNotNull(ordinaryEvent);

        ordinaryEvent!.ClaimedAt = DateTime.UtcNow.AddMinutes(-20);
        Context.PipelineEvents.Update(ordinaryEvent);
        await Context.SaveChangesAsync();

        await queue.ReleaseStuckEventsAsync(TimeSpan.FromMinutes(30));

        var ordinaryStillProcessing = await Context.PipelineEvents
            .AsNoTracking()
            .SingleAsync(e => e.Id == ordinaryEvent.Id);
        Assert.AreEqual("processing", ordinaryStillProcessing.Status,
            "Ordinary claims newer than the short timeout must remain processing.");

        ordinaryEvent.ClaimedAt = DateTime.UtcNow.AddMinutes(-45);
        Context.PipelineEvents.Update(ordinaryEvent);
        await Context.SaveChangesAsync();

        await queue.ReleaseStuckEventsAsync(TimeSpan.FromMinutes(30));

        var ordinaryReleased = await Context.PipelineEvents
            .AsNoTracking()
            .SingleAsync(e => e.Id == ordinaryEvent.Id);
        Assert.AreEqual("pending", ordinaryReleased.Status,
            "Ordinary claims older than the short timeout must be released for retry.");

        // Failed events remain pending but are not immediately claimable.
        ordinaryEvent = await queue.ClaimNextPendingEventAsync("ordinary-worker", eventTypes: ["studies.discovered"]);
        Assert.IsNotNull(ordinaryEvent);
        await queue.FailEventAsync(ordinaryEvent!.Id, "transient failure");
        Assert.IsTrue(await queue.HasActiveEventAsync("studies.discovered"));

        var beforeBackoff = await queue.ClaimNextPendingEventAsync(
            "ordinary-worker",
            eventTypes: ["studies.discovered"]);
        Assert.IsNull(beforeBackoff, "A failed event must wait before its first retry.");

        var retrying = await Context.PipelineEvents.SingleAsync(e => e.Id == ordinaryEvent.Id);
        retrying.LastErrorAt = DateTime.UtcNow.AddSeconds(-31);
        await Context.SaveChangesAsync();

        ordinaryEvent = await queue.ClaimNextPendingEventAsync("ordinary-worker", eventTypes: ["studies.discovered"]);
        Assert.IsNotNull(ordinaryEvent);
        await queue.FailEventAsync(ordinaryEvent!.Id, "transient failure again");

        var beforeSecondBackoff = await queue.ClaimNextPendingEventAsync(
            "ordinary-worker",
            eventTypes: ["studies.discovered"]);
        Assert.IsNull(beforeSecondBackoff, "A second failed event must wait for the longer retry delay.");

        retrying = await Context.PipelineEvents.SingleAsync(e => e.Id == ordinaryEvent.Id);
        retrying.LastErrorAt = DateTime.UtcNow.AddMinutes(-2).AddSeconds(-1);
        await Context.SaveChangesAsync();

        ordinaryEvent = await queue.ClaimNextPendingEventAsync("ordinary-worker", eventTypes: ["studies.discovered"]);
        Assert.IsNotNull(ordinaryEvent);
        await queue.FailEventAsync(ordinaryEvent!.Id, "transient failure a third time");

        var beforeThirdBackoff = await queue.ClaimNextPendingEventAsync(
            "ordinary-worker",
            eventTypes: ["studies.discovered"]);
        Assert.IsNull(beforeThirdBackoff, "A third failed event must wait for the capped retry delay.");

        retrying = await Context.PipelineEvents.SingleAsync(e => e.Id == ordinaryEvent.Id);
        retrying.LastErrorAt = DateTime.UtcNow.AddMinutes(-10).AddSeconds(-1);
        await Context.SaveChangesAsync();

        ordinaryEvent = await queue.ClaimNextPendingEventAsync("ordinary-worker", eventTypes: ["studies.discovered"]);
        Assert.IsNotNull(ordinaryEvent);
        await queue.FailEventAsync(ordinaryEvent!.Id, "permanent failure");

        var deadLettered = await Context.PipelineEvents.AsNoTracking().SingleAsync(e => e.Id == ordinaryEvent.Id);
        Assert.AreEqual("dead-letter", deadLettered.Status);
        Assert.IsFalse(await queue.HasActiveEventAsync("studies.discovered"));
        Assert.IsTrue(
            await queue.HasUnresolvedDiscoveryEventAsync(ordinaryWindowStart),
            "A dead-lettered bounded window must block duplicate discovery events until manually retried.");

        // A claim older than the backfill-specific timeout IS released.
        @event.ClaimedAt = DateTime.UtcNow.AddHours(-13);
        Context.PipelineEvents.Update(@event);
        await Context.SaveChangesAsync();

        await queue.ReleaseStuckEventsAsync(TimeSpan.FromMinutes(30));

        var released = await Context.PipelineEvents.AsNoTracking().SingleAsync(e => e.Id == @event.Id);
        Assert.AreEqual("pending", released.Status,
            "A backfill claim older than the backfill timeout must be released for retry.");

        // 4. Reclaim and ingest the chunk window through the real fetch → upsert path.
        @event = await queue.ClaimNextPendingEventAsync("test-worker", eventTypes: ["studies.backfill"]);
        Assert.IsNotNull(@event);

        var handler = new FixturePageHandler();
        var client = new ClinicalTrialsGov(
            new HttpClient(handler) { BaseAddress = new Uri("https://clinicaltrials.gov/api/v2/") },
            pageSize: 2);
        var ingestion = new ClinicalTrialsIngestionService(client, new StudyRepository(ConnectionString));

        await ingestion.IngestAsync(
            parsed!.Count,
            lastUpdatedPost: parsed.DateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            lastUpdatedPostTo: parsed.DateTo.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        await queue.CompleteEventAsync(@event!.Id);

        var studyCount = await Context.Studies.CountAsync();
        Assert.AreEqual(4, studyCount, "The chunk window must upsert all 4 fixture studies.");
        var completed = await Context.PipelineEvents.AsNoTracking().SingleAsync(e => e.Id == @event.Id);
        Assert.AreEqual("completed", completed.Status);

        // The real event processor must fetch the bounded window and acknowledge
        // the source cursor only after ingestion succeeds.
        var stateService = new DataSourceStateService(ConnectionString);
        var discoveryFrom = new DateTime(2026, 8, 7, 20, 0, 0, DateTimeKind.Utc);
        var discoveryTo = new DateTime(2026, 8, 7, 21, 0, 0, DateTimeKind.Utc);
        await stateService.UpdateLastSyncAsync("ClinicalTrials.gov", discoveryFrom);
        var discoveryPayload = new IncrementalDiscoveryEventPayload(1, discoveryFrom, discoveryTo).ToJson();
        await queue.EnqueueAsync("studies.discovered", discoveryPayload);

        var discoveryHandler = new FixturePageHandler();
        var discoveryClient = new ClinicalTrialsGov(
            new HttpClient(discoveryHandler)
            {
                BaseAddress = new Uri("https://clinicaltrials.gov/api/v2/")
            },
            pageSize: 2);
        var discoveryIngestion = new ClinicalTrialsIngestionService(
            discoveryClient,
            new StudyRepository(ConnectionString));
        using var processor = new EventProcessingService(
            queue,
            stateService,
            discoveryIngestion,
            pollIntervalSeconds: 1);
        using var processorCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await processor.StartAsync(processorCts.Token);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline)
            {
                var current = await Context.PipelineEvents
                    .AsNoTracking()
                    .SingleAsync(e => e.EventType == "studies.discovered" && e.Data == discoveryPayload);
                if (current.Status == "completed")
                {
                    break;
                }

                await Task.Delay(50);
            }
        }
        finally
        {
            await processor.StopAsync(CancellationToken.None);
        }

        var processedDiscovery = await Context.PipelineEvents
            .AsNoTracking()
            .SingleAsync(e => e.EventType == "studies.discovered" && e.Data == discoveryPayload);
        Assert.AreEqual("completed", processedDiscovery.Status);

        var stateAfterDiscovery = await stateService.GetStateAsync("ClinicalTrials.gov");
        Assert.IsNotNull(stateAfterDiscovery);
        Assert.IsNotNull(stateAfterDiscovery!.LastSyncTimestamp);
        Assert.IsTrue(
            Math.Abs((stateAfterDiscovery.LastSyncTimestamp!.Value - discoveryTo).TotalMilliseconds) < 10,
            "The cursor must be acknowledged at the event window end after successful ingestion.");
        Assert.IsTrue(
            discoveryHandler.Requests.Any(request => request.Query.Contains("2026-08-07", StringComparison.Ordinal)),
            "Discovery ingestion must fetch the bounded event window instead of the listing head.");

        await stateService.UpdateLastSyncAsync("ClinicalTrials.gov", discoveryFrom.AddMinutes(-1));
        var monotonicState = await stateService.GetStateAsync("ClinicalTrials.gov");
        Assert.IsNotNull(monotonicState);
        Assert.AreEqual(
            stateAfterDiscovery.LastSyncTimestamp,
            monotonicState!.LastSyncTimestamp,
            "A late retry must not move the source cursor backwards.");

        var stats = await queue.GetStatsAsync();
        Assert.IsTrue(stats.CompletedCount > 0);
        var breakdown = await queue.GetEventTypeBreakdownAsync();
        Assert.IsTrue(breakdown.Any(item => item.EventType == "studies.backfill"));
    }

    /// <summary>
    /// Serves the two captured fixture pages, keyed on whether the request has a pageToken.
    /// </summary>
    private sealed class FixturePageHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var page = request.RequestUri!.Query.Contains("pageToken", StringComparison.Ordinal) ? 2 : 1;
            var json = await File.ReadAllTextAsync(
                $"Data/ClinicalTrialsGov/studies-page{page}.json", cancellationToken);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        }
    }
}
