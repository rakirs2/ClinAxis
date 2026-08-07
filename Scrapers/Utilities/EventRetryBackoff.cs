namespace Scrapers.Utilities;

internal static class EventRetryBackoff
{
    internal static TimeSpan GetDelay(int retryCount)
    {
        return retryCount switch
        {
            <= 0 => TimeSpan.Zero,
            1 => TimeSpan.FromSeconds(30),
            2 => TimeSpan.FromMinutes(2),
            _ => TimeSpan.FromMinutes(10)
        };
    }
}
