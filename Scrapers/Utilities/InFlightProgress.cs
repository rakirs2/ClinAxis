namespace Scrapers.Utilities;

/// <summary>
/// Pure math for deriving live progress (percent, rate, ETA) from the persisted
/// in-flight progress columns of a claimed pipeline event. Kept DB-free so the
/// calculations are unit-testable without a database.
/// </summary>
public static class InFlightProgress
{
    /// <summary>
    /// Derives progress display values for an in-flight event.
    /// </summary>
    /// <param name="processed">Cumulative units of work persisted so far.</param>
    /// <param name="total">Total units of work for the event.</param>
    /// <param name="claimedAt">When the event was claimed. Anchors the average rate.</param>
    /// <param name="now">Current UTC time.</param>
    /// <returns>Percent (0-100) when a total is known, otherwise null; average rate in
    /// units/minute (null until progress is reported); ETA in UTC (null until a rate
    /// exists, or once progress is complete).</returns>
    public static (double? Percent, double? RatePerMin, DateTime? EtaUtc) Calculate(
        int? processed,
        int? total,
        DateTime? claimedAt,
        DateTime now)
    {
        if (processed is null or < 0)
        {
            processed = 0;
        }

        double? percent = null;
        if (total is > 0)
        {
            percent = Math.Min(100.0, processed.Value * 100.0 / total.Value);
        }

        double? ratePerMin = null;
        if (processed > 0 && claimedAt.HasValue)
        {
            var elapsedMinutes = Math.Max((now - claimedAt.Value).TotalMinutes, 0.0001);
            ratePerMin = processed.Value / elapsedMinutes;
        }

        DateTime? etaUtc = null;
        if (ratePerMin is > 0 && total is > 0)
        {
            var remaining = total.Value - processed.Value;
            if (remaining > 0)
            {
                etaUtc = now.AddMinutes(remaining / ratePerMin.Value);
            }
        }

        return (percent, ratePerMin, etaUtc);
    }
}
