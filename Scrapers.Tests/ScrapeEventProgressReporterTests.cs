using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services;

namespace Scrapers.Tests;

[TestClass]
public sealed class ScrapeEventProgressReporterTests
{
    [TestMethod]
    public void BuildDiscoveryEvent_SetsFields()
    {
        var since = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        var evt = ScrapeEventProgressReporter.BuildDiscoveryEvent("ClinicalTrials.gov", 412, since);

        Assert.AreEqual("ClinicalTrials.gov", evt.Source);
        Assert.AreEqual(ScrapeProgressAggregator.DiscoveryEventType, evt.EventType);
        Assert.AreEqual(412, evt.RecordsAffected);
        StringAssert.Contains(evt.Message!, "2026-07-01", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildDiscoveryEvent_NoSince_UsesFullRescanMessage()
    {
        var evt = ScrapeEventProgressReporter.BuildDiscoveryEvent("ClinicalTrials.gov", 10, null);

        Assert.AreEqual("Full re-scan", evt.Message);
    }

    [TestMethod]
    public void BuildBatchEvent_SetsFields()
    {
        var batch = new IngestBatchInfo(
            BatchNumber: 2,
            RecordsProcessed: 25,
            Elapsed: TimeSpan.FromSeconds(3.5),
            NctIds: new[] { "NCT001", "NCT002", "NCT003" });

        var evt = ScrapeEventProgressReporter.BuildBatchEvent("ClinicalTrials.gov", batch);

        Assert.AreEqual(ScrapeProgressAggregator.BatchEventType, evt.EventType);
        Assert.AreEqual(25, evt.RecordsAffected);
        Assert.AreEqual(3500, evt.DurationMs);
        Assert.AreEqual("NCT001 ... NCT003", evt.Message);
    }

    [TestMethod]
    public void BuildRunEvent_SetsFields()
    {
        var evt = ScrapeEventProgressReporter.BuildRunEvent("ClinicalTrials.gov", 250, TimeSpan.FromMinutes(4));

        Assert.AreEqual(ScrapeProgressAggregator.RunCompletedEventType, evt.EventType);
        Assert.AreEqual(250, evt.RecordsAffected);
        Assert.AreEqual(240000, evt.DurationMs);
    }

    [TestMethod]
    public void BuildNctRange_SingleId_ReturnsId()
    {
        Assert.AreEqual("NCT001", ScrapeEventProgressReporter.BuildNctRange(new[] { "NCT001" }));
    }

    [TestMethod]
    public void BuildNctRange_Empty_ReturnsEmptyString()
    {
        Assert.AreEqual(string.Empty, ScrapeEventProgressReporter.BuildNctRange(Array.Empty<string>()));
    }

    [TestMethod]
    public void BuildNctRange_LongId_IsTruncatedTo200Chars()
    {
        var longId = "NCT" + new string('x', 250);
        var range = ScrapeEventProgressReporter.BuildNctRange(new[] { longId });

        Assert.AreEqual(200, range.Length);
        StringAssert.StartsWith(range, "NCT", StringComparison.Ordinal);
    }
}
