using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;
using Scrapers.Services;

namespace Scrapers.Tests;

[TestClass]
public sealed class ScrapeProgressAggregatorTests
{
    private static ScrapeEventEntity Event(int id, string source, string type, int? records = null, long? durationMs = null, DateTime? timestamp = null, string? message = null)
    {
        return new ScrapeEventEntity
        {
            Id = id,
            Source = source,
            EventType = type,
            RecordsAffected = records,
            DurationMs = durationMs,
            Timestamp = timestamp ?? DateTime.UtcNow,
            Message = message
        };
    }

    [TestMethod]
    public void Summarize_NoEvents_ReturnsNull()
    {
        Assert.IsNull(ScrapeProgressAggregator.Summarize(Array.Empty<ScrapeEventEntity>(), "ClinicalTrials.gov"));
    }

    [TestMethod]
    public void Summarize_NoDiscoveryEvent_ReturnsNull()
    {
        var events = new[]
        {
            Event(1, "ClinicalTrials.gov", ScrapeProgressAggregator.BatchEventType, records: 10)
        };

        Assert.IsNull(ScrapeProgressAggregator.Summarize(events, "ClinicalTrials.gov"));
    }

    [TestMethod]
    public void Summarize_DiscoveryOnly_IsPending()
    {
        var discovery = Event(1, "ClinicalTrials.gov", ScrapeProgressAggregator.DiscoveryEventType, records: 5000);
        var summary = ScrapeProgressAggregator.Summarize(new[] { discovery }, "ClinicalTrials.gov");

        Assert.IsNotNull(summary);
        Assert.AreEqual(5000, summary.TotalInRun);
        Assert.AreEqual(0, summary.RecordsProcessed);
        Assert.AreEqual(5000, summary.EstimatedRemaining);
        Assert.AreEqual(0, summary.BatchesCompleted);
        Assert.AreEqual("pending", summary.Status);
    }

    [TestMethod]
    public void Summarize_DiscoveryPlusPartialBatches_IsInProgress()
    {
        var events = new[]
        {
            Event(1, "ClinicalTrials.gov", ScrapeProgressAggregator.DiscoveryEventType, records: 100),
            Event(2, "ClinicalTrials.gov", ScrapeProgressAggregator.BatchEventType, records: 30, durationMs: 2500, message: "NCT001 ... NCT030"),
            Event(3, "ClinicalTrials.gov", ScrapeProgressAggregator.BatchEventType, records: 30, durationMs: 3100, message: "NCT031 ... NCT060")
        };

        var summary = ScrapeProgressAggregator.Summarize(events, "ClinicalTrials.gov");

        Assert.IsNotNull(summary);
        Assert.AreEqual(100, summary.TotalInRun);
        Assert.AreEqual(60, summary.RecordsProcessed);
        Assert.AreEqual(40, summary.EstimatedRemaining);
        Assert.AreEqual(2, summary.BatchesCompleted);
        Assert.AreEqual("in_progress", summary.Status);
        Assert.AreEqual("NCT031 ... NCT060", summary.LastBatchNctRange);
        Assert.AreEqual(3100, summary.LastBatchDurationMs);
        Assert.AreEqual(30, summary.LastBatchRecords);
    }

    [TestMethod]
    public void Summarize_RunCompletedEvent_MarksCompleted()
    {
        var events = new[]
        {
            Event(1, "ClinicalTrials.gov", ScrapeProgressAggregator.DiscoveryEventType, records: 100),
            Event(2, "ClinicalTrials.gov", ScrapeProgressAggregator.BatchEventType, records: 100),
            Event(3, "ClinicalTrials.gov", ScrapeProgressAggregator.RunCompletedEventType, records: 100)
        };

        var summary = ScrapeProgressAggregator.Summarize(events, "ClinicalTrials.gov");

        Assert.IsNotNull(summary);
        Assert.AreEqual("completed", summary.Status);
        Assert.AreEqual(0, summary.EstimatedRemaining);
    }

    [TestMethod]
    public void Summarize_ProcessedReachesTotal_MarksCompletedWithoutRunEvent()
    {
        var events = new[]
        {
            Event(1, "ClinicalTrials.gov", ScrapeProgressAggregator.DiscoveryEventType, records: 100),
            Event(2, "ClinicalTrials.gov", ScrapeProgressAggregator.BatchEventType, records: 100)
        };

        var summary = ScrapeProgressAggregator.Summarize(events, "ClinicalTrials.gov");

        Assert.IsNotNull(summary);
        Assert.AreEqual("completed", summary.Status);
    }

    [TestMethod]
    public void Summarize_OnlyCountsMatchingSource()
    {
        var events = new[]
        {
            Event(1, "PubMed", ScrapeProgressAggregator.DiscoveryEventType, records: 999),
            Event(2, "ClinicalTrials.gov", ScrapeProgressAggregator.DiscoveryEventType, records: 50),
            Event(3, "PubMed", ScrapeProgressAggregator.BatchEventType, records: 999),
            Event(4, "ClinicalTrials.gov", ScrapeProgressAggregator.BatchEventType, records: 20)
        };

        var summary = ScrapeProgressAggregator.Summarize(events, "ClinicalTrials.gov");

        Assert.IsNotNull(summary);
        Assert.AreEqual(50, summary.TotalInRun);
        Assert.AreEqual(20, summary.RecordsProcessed);
    }

    [TestMethod]
    public void Summarize_StaleEventsBeforeLatestDiscovery_AreIgnored()
    {
        var events = new[]
        {
            Event(1, "ClinicalTrials.gov", ScrapeProgressAggregator.DiscoveryEventType, records: 500),
            Event(2, "ClinicalTrials.gov", ScrapeProgressAggregator.BatchEventType, records: 500),
            Event(3, "ClinicalTrials.gov", ScrapeProgressAggregator.RunCompletedEventType, records: 500),
            Event(4, "ClinicalTrials.gov", ScrapeProgressAggregator.DiscoveryEventType, records: 100),
            Event(5, "ClinicalTrials.gov", ScrapeProgressAggregator.BatchEventType, records: 40)
        };

        var summary = ScrapeProgressAggregator.Summarize(events, "ClinicalTrials.gov");

        Assert.IsNotNull(summary);
        Assert.AreEqual(100, summary.TotalInRun);
        Assert.AreEqual(40, summary.RecordsProcessed);
        Assert.AreEqual(60, summary.EstimatedRemaining);
        Assert.AreEqual("in_progress", summary.Status);
    }

    [TestMethod]
    public void Summarize_DiscoveryWithoutRecordCount_FallsBackToProcessed()
    {
        var events = new[]
        {
            Event(1, "ClinicalTrials.gov", ScrapeProgressAggregator.DiscoveryEventType),
            Event(2, "ClinicalTrials.gov", ScrapeProgressAggregator.BatchEventType, records: 75)
        };

        var summary = ScrapeProgressAggregator.Summarize(events, "ClinicalTrials.gov");

        Assert.IsNotNull(summary);
        Assert.AreEqual(75, summary.TotalInRun);
        Assert.AreEqual(0, summary.EstimatedRemaining);
    }
}
