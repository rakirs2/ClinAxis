namespace Scrapers.Services.EventQueue;

public static class DurationPercentileCalculator
{
    public static DurationPercentiles ComputePercentiles(IReadOnlyList<double> durations)
    {
        ArgumentNullException.ThrowIfNull(durations);
        var sorted = durations.OrderBy(d => d).ToList();
        if (sorted.Count == 0)
            return new DurationPercentiles { Count = 0 };
        return new DurationPercentiles
        {
            Count = sorted.Count,
            MinMs = sorted[0],
            P50Ms = Percentile(sorted, 50),
            P95Ms = Percentile(sorted, 95),
            P99Ms = Percentile(sorted, 99),
            MaxMs = sorted[^1]
        };
    }

    public static double Percentile(IReadOnlyList<double> sorted, int percentile)
    {
        ArgumentNullException.ThrowIfNull(sorted);
        if (sorted.Count == 0) return 0;
        if (sorted.Count == 1) return sorted[0];

        double rank = (percentile / 100.0) * (sorted.Count - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);

        if (lower == upper) return sorted[lower];

        double frac = rank - lower;
        return sorted[lower] + frac * (sorted[upper] - sorted[lower]);
    }
}