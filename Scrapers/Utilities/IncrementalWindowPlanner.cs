namespace Scrapers.Utilities;

/// <summary>
/// Plans bounded incremental ClinicalTrials.gov windows so a changing result set does not
/// require one long-lived pagination sequence.
/// </summary>
public static class IncrementalWindowPlanner
{
    private static readonly TimeSpan MaxWindow = TimeSpan.FromHours(24);

    public static DateTime NextWindowEnd(DateTime? lastSyncUtc, DateTime nowUtc)
    {
        if (!lastSyncUtc.HasValue)
        {
            return nowUtc;
        }

        if (nowUtc < lastSyncUtc.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(nowUtc), "The current time cannot precede the last sync time.");
        }

        return Min(lastSyncUtc.Value.Add(MaxWindow), nowUtc);
    }

    private static DateTime Min(DateTime left, DateTime right) => left <= right ? left : right;
}
