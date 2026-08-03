namespace Scrapers.Utilities;

/// <summary>
/// One date-window of a full-corpus sweep over <c>LastUpdatePostDate</c>, oldest first.
/// The window is inclusive on both ends and uses date-only granularity (the CT.gov
/// API rejects finer bounds).
/// </summary>
public sealed record BackfillChunk(DateOnly DateFrom, DateOnly DateTo, int Count);

/// <summary>
/// Plans a full-corpus backfill as date-window chunks, each bounded to at most
/// <paramref name="maxChunkStudies"/> studies. Windows are walked oldest month first;
/// any month whose count exceeds the cap is recursively split until every chunk fits.
/// Zero-count windows are skipped. The planner is pure (no DB, no HTTP): counting is
/// injected, so it is fully unit-testable.
/// </summary>
public static class BackfillChunkPlanner
{
    /// <summary>
    /// Builds the ordered chunk list covering <c>[earliestDate, latestDate]</c> inclusive.
    /// </summary>
    /// <param name="countRange">Counts studies in the inclusive date window; must match the
    /// API's date-only semantics.</param>
    /// <param name="earliestDate">First day to consider (inclusive).</param>
    /// <param name="latestDate">Last day to consider (inclusive).</param>
    /// <param name="maxChunkStudies">Maximum studies per chunk; windows larger than this are
    /// split. Must be &gt; 0.</param>
    public static async Task<IReadOnlyList<BackfillChunk>> PlanAsync(
        Func<DateOnly, DateOnly, CancellationToken, Task<int>> countRange,
        DateOnly earliestDate,
        DateOnly latestDate,
        int maxChunkStudies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countRange);
        if (maxChunkStudies <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChunkStudies), "maxChunkStudies must be greater than 0.");
        }

        var chunks = new List<BackfillChunk>();
        if (earliestDate > latestDate)
        {
            return chunks;
        }

        var cursor = earliestDate;
        while (cursor <= latestDate)
        {
            var monthEnd = MonthEnd(new DateOnly(cursor.Year, cursor.Month, 1));
            var windowEnd = monthEnd < latestDate ? monthEnd : latestDate;
            var count = await countRange(cursor, windowEnd, cancellationToken).ConfigureAwait(false);

            if (count > 0)
            {
                await AddWindowAsync(chunks, countRange, cursor, windowEnd, maxChunkStudies, cancellationToken)
                    .ConfigureAwait(false);
            }

            cursor = windowEnd.AddDays(1);
        }

        return chunks;
    }

    private static async Task AddWindowAsync(
        List<BackfillChunk> chunks,
        Func<DateOnly, DateOnly, CancellationToken, Task<int>> countRange,
        DateOnly from,
        DateOnly to,
        int maxChunkStudies,
        CancellationToken cancellationToken)
    {
        if (from > to)
        {
            return;
        }

        var count = await countRange(from, to, cancellationToken).ConfigureAwait(false);
        if (count == 0)
        {
            return;
        }

        if (count <= maxChunkStudies || from == to)
        {
            chunks.Add(new BackfillChunk(from, to, count));
            return;
        }

        var midPoint = from.AddDays((int)((to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue)).TotalDays / 2));
        await AddWindowAsync(chunks, countRange, from, midPoint, maxChunkStudies, cancellationToken).ConfigureAwait(false);
        await AddWindowAsync(chunks, countRange, midPoint.AddDays(1), to, maxChunkStudies, cancellationToken).ConfigureAwait(false);
    }

    private static DateOnly MonthEnd(DateOnly monthStart)
    {
        var nextMonth = monthStart.AddMonths(1);
        return new DateOnly(nextMonth.Year, nextMonth.Month, 1).AddDays(-1);
    }
}
