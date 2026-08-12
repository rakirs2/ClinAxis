namespace MeshBench;

public static class BenchmarkStatistics
{
    public static double Percentile(IReadOnlyList<double> samples, double percentile)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
            throw new ArgumentException("At least one sample is required", nameof(samples));
        if (percentile is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percentile));

        var sorted = samples.Order().ToArray();
        double position = percentile / 100 * (sorted.Length - 1);
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        if (lower == upper)
            return sorted[lower];

        double fraction = position - lower;
        return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
    }
}
