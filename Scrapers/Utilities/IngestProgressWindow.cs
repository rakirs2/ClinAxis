using System;

namespace Scrapers.Utilities;

/// <summary>
/// Pure window computation for ingest progress metrics. The boundary instant is derived from a
/// caller-supplied "now" so it can be unit-tested without a clock or a database.
/// </summary>
public static class IngestProgressWindow
{
    /// <summary>
    /// The UTC instant that starts a rolling 24-hour window (now minus 24 hours).
    /// </summary>
    public static DateTime Last24Hours(DateTime nowUtc)
    {
        return nowUtc.AddHours(-24);
    }
}
