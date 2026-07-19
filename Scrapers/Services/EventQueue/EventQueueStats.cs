namespace Scrapers.Services.EventQueue;

public sealed class EventQueueStats
{
    public int PendingCount { get; set; }
    public int ProcessingCount { get; set; }
    public int CompletedCount { get; set; }
    public int DeadLetterCount { get; set; }
    public double AverageProcessingTimeMs { get; set; }
    public double FailureRate { get; set; }
    public int FailedCount { get; set; }
    public double? EstimatedTimeRemainingMs { get; set; }
}

public sealed class EventTypeBreakdown
{
    public string EventType { get; set; } = string.Empty;
    public int Pending { get; set; }
    public int Processing { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int DeadLetter { get; set; }
    public double AverageProcessingTimeMs { get; set; }
    public DurationPercentiles? Percentiles { get; set; }
}

public sealed class DurationPercentiles
{
    public int Count { get; set; }
    public double MinMs { get; set; }
    public double P50Ms { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public double MaxMs { get; set; }
}

public sealed class DurationHistoryPoint
{
    public DateTime Bucket { get; set; }
    public int Count { get; set; }
    public double AverageMs { get; set; }
    public double P50Ms { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
}
