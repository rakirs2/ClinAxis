using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Services;

namespace Scrapers.Tests;

[TestClass]
public sealed class ScrapeEtaCalculatorTests
{
    [TestMethod]
    public void RatePerHour_3600RecordsInOneHour_Is3600()
    {
        var rate = ScrapeEtaCalculator.RatePerHour(3600, 3600000);

        Assert.IsNotNull(rate);
        Assert.AreEqual(3600.0, rate.Value, 0.001);
    }

    [TestMethod]
    public void RatePerHour_5000RecordsInThirtyMinutes_Is10000()
    {
        var rate = ScrapeEtaCalculator.RatePerHour(5000, 1800000);

        Assert.IsNotNull(rate);
        Assert.AreEqual(10000.0, rate.Value, 0.001);
    }

    [TestMethod]
    public void RatePerHour_NoRecords_ReturnsNull()
    {
        Assert.IsNull(ScrapeEtaCalculator.RatePerHour(0, 3600000));
    }

    [TestMethod]
    public void RatePerHour_NullOrZeroDuration_ReturnsNull()
    {
        Assert.IsNull(ScrapeEtaCalculator.RatePerHour(100, null));
        Assert.IsNull(ScrapeEtaCalculator.RatePerHour(100, 0));
    }

    [TestMethod]
    public void Calculate_NoObservedRate_ReturnsNull()
    {
        Assert.IsNull(ScrapeEtaCalculator.Calculate(totalAvailable: 10000, totalInDb: 500, recordsProcessed: 0, durationMs: null));
    }

    [TestMethod]
    public void Calculate_NothingRemaining_ReturnsNull()
    {
        Assert.IsNull(ScrapeEtaCalculator.Calculate(totalAvailable: 10000, totalInDb: 10000, recordsProcessed: 10000, durationMs: 3600000));
    }

    [TestMethod]
    public void Calculate_ProjectedCompletion_IsInFutureAndPlausible()
    {
        // 6000/hr rate, 3000 remaining -> 0.5 hours
        var eta = ScrapeEtaCalculator.Calculate(totalAvailable: 10000, totalInDb: 7000, recordsProcessed: 6000, durationMs: 3600000);

        Assert.IsNotNull(eta);
        Assert.AreEqual(3000, eta.RemainingStudies);
        Assert.AreEqual(6000.0, eta.RatePerHour, 0.001);
        var expected = DateTime.UtcNow.AddMinutes(30);
        Assert.IsTrue(eta.EstimatedCompletionUtc > DateTime.UtcNow.AddMinutes(29) &&
                      eta.EstimatedCompletionUtc < DateTime.UtcNow.AddMinutes(31),
            $"Expected completion ~30min out, got {eta.EstimatedCompletionUtc:O}");
    }

    [TestMethod]
    public void Calculate_MoreInDbThanAvailable_ReturnsNull()
    {
        Assert.IsNull(ScrapeEtaCalculator.Calculate(totalAvailable: 100, totalInDb: 150, recordsProcessed: 100, durationMs: 3600000));
    }
}
