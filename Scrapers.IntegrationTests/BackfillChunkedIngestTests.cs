using System.Net;
using System.Net.Http.Headers;
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
    }

    /// <summary>
    /// Serves the two captured fixture pages, keyed on whether the request has a pageToken.
    /// </summary>
    private sealed class FixturePageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var page = request.RequestUri!.Query.Contains("pageToken", StringComparison.Ordinal) ? 2 : 1;
            var json = await File.ReadAllTextAsync(
                $"Data/ClinicalTrialsGov/studies-page{page}.json", cancellationToken);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        }
    }
}
