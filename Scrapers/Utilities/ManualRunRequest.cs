namespace Scrapers.Utilities;

/// <summary>
/// Validates the mode parameter of the manual ingest trigger (POST /api/ingest/run).
/// Pure and unit-testable; the endpoint and scrape loop both consume it.
/// </summary>
public static class ManualRunRequest
{
    /// <summary>Trigger an incremental scrape now (same window semantics as the scheduled run).</summary>
    public const string IncrementalMode = "incremental";

    /// <summary>Trigger a full-corpus re-sync (forces a fresh sweep over every date window).</summary>
    public const string FullMode = "full";

    /// <summary>
    /// Returns true when <paramref name="mode"/> is a supported manual run mode
    /// ("incremental" or "full", case-insensitive).
    /// </summary>
    public static bool IsValidMode(string? mode)
    {
        return string.Equals(mode, IncrementalMode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, FullMode, StringComparison.OrdinalIgnoreCase);
    }
}
