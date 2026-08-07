using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class EventRetryBackoffTests
{
    [TestMethod]
    public void GetDelay_UsesDocumentedCappedSchedule()
    {
        Assert.AreEqual(TimeSpan.Zero, EventRetryBackoff.GetDelay(0));
        Assert.AreEqual(TimeSpan.FromSeconds(30), EventRetryBackoff.GetDelay(1));
        Assert.AreEqual(TimeSpan.FromMinutes(2), EventRetryBackoff.GetDelay(2));
        Assert.AreEqual(TimeSpan.FromMinutes(10), EventRetryBackoff.GetDelay(3));
        Assert.AreEqual(TimeSpan.FromMinutes(10), EventRetryBackoff.GetDelay(4));
    }
}
