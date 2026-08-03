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
        await queue.CompleteEventAsync(first!.Id);

        var toDeadLetter = await queue.ClaimNextPendingEventAsync("test-worker", eventTypes: ["studies.backfill"]);
        Assert.IsNotNull(toDeadLetter);
        await queue.FailEventAsync(toDeadLetter!.Id, "test failure 1");
        await queue.FailEventAsync(toDeadLetter.Id, "test failure 2");
        await queue.FailEventAsync(toDeadLetter.Id, "test failure 3");

        var snapshot = await coordinator.GetChunkQueueSnapshotAsync();
        Assert.AreEqual(1, snapshot.PendingOrProcessing, "One chunk remains pending.");
        Assert.AreEqual(1, snapshot.DeadLettered, "One chunk dead-lettered after 3 failures.");

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
    }
}
