namespace Scrapers.Services.EventQueue;

/// <summary>
/// Statistics about the event queue health and performance.
/// </summary>
public sealed class EventQueueStats
{
    public int PendingCount { get; set; }
    public int ProcessingCount { get; set; }
    public int CompletedCount { get; set; }
    public int DeadLetterCount { get; set; }
    public double AverageProcessingTimeMs { get; set; }
    public double FailureRate { get; set; }  // 0-1 (percentage of failed events)
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
}
