using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Services.Backfill;
using Scrapers.Services.EventQueue;
using Scrapers.Testing;
using Scrapers.Utilities;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class BackfillLifecycleTests : DbTestBase
{
    private const string Payload = """{"chunkIndex":0,"count":100,"dateFrom":"2026-01-01","dateTo":"2026-01-31","sweepStartedUtc":"2026-08-03T12:00:00Z"}""";

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ReconcileAndComplete_FlagsStudiesMissingFromCompletedSweep()
    {
        var sweepStart = DateTime.UtcNow.AddHours(-1);
        var earlierSweep = sweepStart.AddDays(-2);

        Context.Studies.AddRange(
            new StudyEntity { NctId = "NCT-SEEN-CURRENT", LastSeenInSweepUtc = sweepStart },
            new StudyEntity { NctId = "NCT-SEEN-OLD", LastSeenInSweepUtc = earlierSweep },
            new StudyEntity { NctId = "NCT-NEVER-SEEN" },
            new StudyEntity { NctId = "NCT-ALREADY-REMOVED", LastSeenInSweepUtc = sweepStart, RemovedFromSourceAt = DateTime.UtcNow.AddDays(-3) });
        await Context.SaveChangesAsync();

        var coordinator = new BackfillCoordinatorService(ConnectionString);
        var flagged = await coordinator.ReconcileAndCompleteAsync(sweepStart);

        Assert.AreEqual(2, flagged, "Only the old-stamp and never-stamped studies are newly flagged.");

        var studies = await Context.Studies.AsNoTracking().ToListAsync();
        Assert.IsNull(studies.Single(s => s.NctId == "NCT-SEEN-CURRENT").RemovedFromSourceAt,
            "Studies re-fetched during the sweep stay live.");
        Assert.IsNotNull(studies.Single(s => s.NctId == "NCT-SEEN-OLD").RemovedFromSourceAt,
            "Studies only seen in an earlier sweep are removed.");
        Assert.IsNotNull(studies.Single(s => s.NctId == "NCT-NEVER-SEEN").RemovedFromSourceAt,
            "Studies never fetched by any sweep are removed.");
        Assert.AreEqual(DateTime.UtcNow.AddDays(-3).Date, studies.Single(s => s.NctId == "NCT-ALREADY-REMOVED").RemovedFromSourceAt!.Value.Date,
            "Already-removed studies are not re-stamped.");

        // A second reconciliation must be a no-op (idempotent).
        var secondRun = await coordinator.ReconcileAndCompleteAsync(sweepStart);
        Assert.AreEqual(0, secondRun);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ChunkQueueTracking_AndBackfillState_ReflectSweepProgress()
    {
        var queue = new EventQueueService(ConnectionString);
        var coordinator = new BackfillCoordinatorService(ConnectionString);
        var stateService = new DataSourceStateService(ConnectionString);

        // 3 chunks: one completes, one stays pending, one dead-letters.
        for (var i = 0; i < 3; i++)
        {
            await queue.EnqueueAsync("studies.backfill", Payload);
        }

        var first = await queue.ClaimNextPendingEventAsync("test-worker", eventTypes: ["studies.backfill"]);
        Assert.IsNotNull(first);

        // In-flight progress must surface in the breakdown while the event is processing.
        await queue.UpdateEventProgressAsync(first!.Id, 50, 100);
        var inFlightBreakdown = await queue.GetEventTypeBreakdownAsync();
        var inFlightBackfill = inFlightBreakdown.Single(et => et.EventType == "studies.backfill");
        Assert.IsNotNull(inFlightBackfill.InFlight, "The claimed backfill event must appear as in-flight.");
        Assert.AreEqual(first.Id, inFlightBackfill.InFlight!.EventId);
        Assert.AreEqual(50, inFlightBackfill.InFlight.Processed);
        Assert.AreEqual(100, inFlightBackfill.InFlight.Total);
        Assert.AreEqual(50.0, inFlightBackfill.InFlight.Percent!.Value, 0.001);
        Assert.IsNotNull(inFlightBackfill.InFlight.RatePerMin, "A claimed event with progress must yield a rate.");

        await queue.CompleteEventAsync(first!.Id);

        var toDeadLetter = await queue.ClaimNextPendingEventAsync("test-worker", eventTypes: ["studies.backfill"]);
        Assert.IsNotNull(toDeadLetter);
        await queue.FailEventAsync(toDeadLetter!.Id, "test failure 1");
        await queue.FailEventAsync(toDeadLetter.Id, "test failure 2");
        await queue.FailEventAsync(toDeadLetter.Id, "test failure 3");
        await queue.FailEventAsync(toDeadLetter.Id, "test failure 4");

        var snapshot = await coordinator.GetChunkQueueSnapshotAsync();
        Assert.AreEqual(1, snapshot.PendingOrProcessing, "One chunk remains pending.");
        Assert.AreEqual(1, snapshot.DeadLettered, "One chunk dead-lettered after 4 failures.");

        var completedWindows = await coordinator.GetCompletedChunkWindowsAsync();
        Assert.AreEqual(1, completedWindows.Count);
        Assert.AreEqual((new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)), completedWindows[0]);

        // Sweep state persists through DataSourceStateService and round-trips.
        var sweepStart = DateTime.UtcNow;
        await stateService.UpdateBackfillStateAsync("ClinicalTrials.gov", "in-progress", 402_024, sweepStart, null);
        var state = await stateService.GetStateAsync("ClinicalTrials.gov");
        Assert.IsNotNull(state);
        Assert.AreEqual("in-progress", state!.BackfillStatus);
        Assert.AreEqual(402_024, state.BackfillRemainingStudies);
        Assert.IsNotNull(state.BackfillStartedUtc);
        Assert.IsNull(state.BackfillCompletedUtc);

        await stateService.SetStatusAsync("ClinicalTrials.gov", "failed", "temporary CT.gov failure");
        await stateService.SetStatusAsync("ClinicalTrials.gov", "syncing");
        state = await stateService.GetStateAsync("ClinicalTrials.gov");
        Assert.IsNotNull(state);
        Assert.AreEqual("syncing", state!.Status);
        Assert.AreEqual("temporary CT.gov failure", state.ErrorMessage,
            "Entering a retrying state must preserve the last failure message.");

        await stateService.UpdateLastSyncAsync("ClinicalTrials.gov", DateTime.UtcNow);
        state = await stateService.GetStateAsync("ClinicalTrials.gov");
        Assert.IsNotNull(state);
        Assert.IsNull(state!.ErrorMessage, "A successful sync must clear the previous failure message.");

        // Bulk DLQ retry (production recovery): type filter resets only matching events;
        // no filter resets everything.
        await queue.EnqueueAsync("investigator.enrichment", "{}");
        var other = await queue.ClaimNextPendingEventAsync("test-worker", eventTypes: ["investigator.enrichment"]);
        Assert.IsNotNull(other);
        await queue.FailEventAsync(other!.Id, "old bug failure 1");
        await queue.FailEventAsync(other.Id, "old bug failure 2");
        await queue.FailEventAsync(other.Id, "old bug failure 3");
        await queue.FailEventAsync(other.Id, "old bug failure 4");

        var filtered = await queue.RetryAllDeadLetterEventsAsync("studies.backfill");
        Assert.AreEqual(1, filtered, "Only studies.backfill dead letters are reset by the type filter.");
        var all = await queue.RetryAllDeadLetterEventsAsync();
        Assert.AreEqual(1, all, "The enrichment dead letter remains until an unfiltered retry.");

        snapshot = await coordinator.GetChunkQueueSnapshotAsync();
        Assert.AreEqual(2, snapshot.PendingOrProcessing, "The reset chunk is claimable again alongside the untouched pending chunk.");
        Assert.AreEqual(0, snapshot.DeadLettered, "No backfill chunks remain dead-lettered after retry.");

        var queueStats = await queue.GetStatsAsync();
        Assert.IsTrue(queueStats.PendingCount >= 2, "Grouped queue counts must include pending events.");
        Assert.AreEqual(0, queueStats.DeadLetterCount, "Grouped queue counts must include the cleared dead-letter state.");

        // Heartbeat: the completed backfill chunk counts within the last 15 minutes,
        // while the enrichment event (never completed) reports zero.
        var heartbeatBreakdown = await queue.GetEventTypeBreakdownAsync();
        var backfillHeartbeat = heartbeatBreakdown.Single(et => et.EventType == "studies.backfill");
        var enrichmentHeartbeat = heartbeatBreakdown.Single(et => et.EventType == "investigator.enrichment");
        Assert.IsTrue(backfillHeartbeat.CompletedLast15m >= 1, "The completed chunk must count toward the 15-minute heartbeat.");
        Assert.IsTrue(backfillHeartbeat.CompletedLast1h >= 1, "The completed chunk must count toward the 1-hour heartbeat.");
        Assert.AreEqual(0, enrichmentHeartbeat.CompletedLast15m, "A type with no completions must report a zero heartbeat.");
        Assert.IsNull(backfillHeartbeat.InFlight, "No in-flight event remains after completion.");
    }
}
