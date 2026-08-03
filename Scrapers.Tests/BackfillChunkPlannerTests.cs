using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class BackfillChunkPlannerTests
{
    private static readonly DateOnly Earliest = new(2026, 1, 1);
    private static readonly DateOnly Latest = new(2026, 3, 31);

    private static Func<DateOnly, DateOnly, CancellationToken, Task<int>> FixedCount(int count) =>
        (_, _, _) => Task.FromResult(count);

    [TestMethod]
    public async Task Plan_UnderCapMonths_ReturnsOneChunkPerMonth()
    {
        var chunks = await BackfillChunkPlanner.PlanAsync(
            FixedCount(5), Earliest, Latest, maxChunkStudies: 100);

        Assert.AreEqual(3, chunks.Count);
        Assert.AreEqual(new BackfillChunk(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 5), chunks[0]);
        Assert.AreEqual(new BackfillChunk(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), 5), chunks[1]);
        Assert.AreEqual(new BackfillChunk(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), 5), chunks[2]);
    }

    [TestMethod]
    public async Task Plan_MidMonthEarliest_StartsWindowAtEarliestDate()
    {
        var earliest = new DateOnly(2026, 1, 10);

        var chunks = await BackfillChunkPlanner.PlanAsync(
            FixedCount(5), earliest, Latest, maxChunkStudies: 100);

        Assert.AreEqual(new DateOnly(2026, 1, 10), chunks[0].DateFrom);
        Assert.AreEqual(new DateOnly(2026, 1, 31), chunks[0].DateTo);
    }

    [TestMethod]
    public async Task Plan_LastMonthTruncated_EndsAtLatestDate()
    {
        var latest = new DateOnly(2026, 3, 20);

        var chunks = await BackfillChunkPlanner.PlanAsync(
            FixedCount(5), Earliest, latest, maxChunkStudies: 100);

        Assert.AreEqual(new DateOnly(2026, 3, 20), chunks[^1].DateTo);
    }

    [TestMethod]
    public async Task Plan_OverCapMonth_IsSplitIntoBoundedContiguousChunks()
    {
        // Count grows with window width, so split chunks genuinely fit under the cap.
        Task<int> CountRange(DateOnly from, DateOnly to, CancellationToken _) =>
            Task.FromResult((to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue)).Days + 1);

        var chunks = await BackfillChunkPlanner.PlanAsync(CountRange, Earliest, new DateOnly(2026, 1, 31), maxChunkStudies: 10);

        Assert.IsTrue(chunks.Count > 1, "Over-cap month must be split.");
        var cursor = Earliest;
        foreach (var chunk in chunks)
        {
            Assert.IsTrue(chunk.Count <= 10, $"Chunk {chunk} exceeds cap.");
            Assert.AreEqual(cursor, chunk.DateFrom, "Chunks must be contiguous and ordered.");
            Assert.IsTrue(chunk.DateTo >= chunk.DateFrom);
            cursor = chunk.DateTo.AddDays(1);
        }

        Assert.AreEqual(cursor.AddDays(-1), new DateOnly(2026, 1, 31), "Chunks must cover the full window.");
    }

    [TestMethod]
    public async Task Plan_ZeroCountMonths_AreSkipped()
    {
        Task<int> CountRange(DateOnly from, DateOnly to, CancellationToken _) =>
            Task.FromResult(from.Month == 2 ? 0 : 5);

        var chunks = await BackfillChunkPlanner.PlanAsync(CountRange, Earliest, Latest, maxChunkStudies: 100);

        Assert.AreEqual(2, chunks.Count);
        Assert.IsTrue(chunks.All(c => c.DateFrom.Month != 2));
    }

    [TestMethod]
    public async Task Plan_EmptyRange_ReturnsEmpty()
    {
        var chunks = await BackfillChunkPlanner.PlanAsync(FixedCount(5), Latest, Earliest, maxChunkStudies: 100);

        Assert.AreEqual(0, chunks.Count);
    }

    [TestMethod]
    public async Task Plan_InvalidMaxChunk_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => BackfillChunkPlanner.PlanAsync(FixedCount(5), Earliest, Latest, maxChunkStudies: 0));
    }
}
