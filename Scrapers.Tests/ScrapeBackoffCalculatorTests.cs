using IngestionApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Scrapers.Tests;

[TestClass]
public sealed class ScrapeBackoffCalculatorTests
{
    [TestMethod]
    public void GetDelay_UsesCappedProgressiveDelays()
    {
        Assert.AreEqual(TimeSpan.Zero, ScrapeBackoffCalculator.GetDelay(0));
        Assert.AreEqual(TimeSpan.FromMinutes(1), ScrapeBackoffCalculator.GetDelay(1));
        Assert.AreEqual(TimeSpan.FromMinutes(5), ScrapeBackoffCalculator.GetDelay(2));
        Assert.AreEqual(TimeSpan.FromMinutes(15), ScrapeBackoffCalculator.GetDelay(3));
        Assert.AreEqual(TimeSpan.FromMinutes(15), ScrapeBackoffCalculator.GetDelay(4));
    }
}
