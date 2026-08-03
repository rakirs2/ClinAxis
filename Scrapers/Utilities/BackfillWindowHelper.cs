namespace Scrapers.Utilities;

/// <summary>
/// Pure helpers for sweep window bookkeeping: deciding whether a planned chunk window is
/// already covered by a previously completed chunk event, so re-enqueues skip completed
/// work after a restart or a partial failure.
/// </summary>
public static class BackfillWindowHelper
{
    /// <summary>
    /// A window is covered when a completed window fully contains it (inclusive bounds).
    /// Partial overlaps are NOT covered — the chunk re-fetches the whole window (idempotent
    /// upserts make the redundant re-fetch safe and simpler than tracking partial progress).
    /// </summary>
    public static bool IsCovered((DateOnly From, DateOnly To) window, IEnumerable<(DateOnly From, DateOnly To)> completedWindows)
    {
        ArgumentNullException.ThrowIfNull(completedWindows);

        foreach (var completed in completedWindows)
        {
            if (window.From >= completed.From && window.To <= completed.To)
            {
                return true;
            }
        }

        return false;
    }
}
