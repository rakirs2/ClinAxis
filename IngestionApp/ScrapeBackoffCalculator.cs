namespace IngestionApp;

internal static class ScrapeBackoffCalculator
{
    internal static TimeSpan GetDelay(int consecutiveFailures)
    {
        return consecutiveFailures switch
        {
            <= 0 => TimeSpan.Zero,
            1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(15)
        };
    }
}
