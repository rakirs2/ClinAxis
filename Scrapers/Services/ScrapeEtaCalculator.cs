using Scrapers.Persistence.Entities;

namespace Scrapers.Services;

/// <summary>
/// Estimated completion for the full clinicaltrials.gov scrape, derived from the
/// observed ingest rate of the most recent completed run (records / wall time).
/// </summary>
/// <param name="RatePerHour">Observed ingest rate (records per hour) of the last completed run.</param>
/// <param name="RemainingStudies">Studies still to ingest (<c>totalAvailable - totalInDb</c>, never negative).</param>
/// <param name="EstimatedCompletionUtc">When the remaining studies would be ingested at the observed rate.</param>
public sealed record ScrapeEta(
    double RatePerHour,
    int RemainingStudies,
    DateTime EstimatedCompletionUtc);

public static class ScrapeEtaCalculator
{
    /// <summary>
    /// Ingest rate in records per hour from a completed run. Returns <c>null</c> when
    /// the run ingested nothing or its duration is unknown/zero.
    /// </summary>
    public static double? RatePerHour(int recordsProcessed, long? durationMs)
    {
        if (recordsProcessed <= 0 || durationMs is not (> 0))
        {
            return null;
        }

        return recordsProcessed / (durationMs.Value / 3600000.0);
    }

    /// <summary>
    /// Computes the estimated completion for a scrape of <paramref name="totalAvailable"/>
    /// studies given <paramref name="totalInDb"/> already ingested and the last completed
    /// run's records/duration. Returns <c>null</c> when there is no observed rate yet or
    /// nothing remains to ingest.
    /// </summary>
    public static ScrapeEta? Calculate(int totalAvailable, int totalInDb, int recordsProcessed, long? durationMs)
    {
        var rate = RatePerHour(recordsProcessed, durationMs);
        if (rate is not (> 0))
        {
            return null;
        }

        var remaining = Math.Max(0, totalAvailable - totalInDb);
        if (remaining == 0)
        {
            return null;
        }

        var hours = remaining / rate.Value;
        return new ScrapeEta(rate.Value, remaining, DateTime.UtcNow.AddHours(hours));
    }
}
