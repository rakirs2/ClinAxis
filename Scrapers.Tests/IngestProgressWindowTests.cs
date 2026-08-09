using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class IngestProgressWindowTests
{
    [TestMethod]
    public void Last24Hours_ReturnsNowMinus24Hours()
    {
        var now = new DateTime(2026, 8, 9, 12, 30, 45, DateTimeKind.Utc);

        var fromUtc = IngestProgressWindow.Last24Hours(now);

        Assert.AreEqual(new DateTime(2026, 8, 8, 12, 30, 45, DateTimeKind.Utc), fromUtc);
    }

    [TestMethod]
    public void Last24Hours_CrossesDayBoundary()
    {
        var now = new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc);

        var fromUtc = IngestProgressWindow.Last24Hours(now);

        Assert.AreEqual(new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc), fromUtc);
    }

    [TestMethod]
    public void Last24Hours_PreservesSubsecondPrecision()
    {
        var now = new DateTime(2026, 8, 9, 1, 2, 3, 400, DateTimeKind.Utc);

        var fromUtc = IngestProgressWindow.Last24Hours(now);

        Assert.AreEqual(new DateTime(2026, 8, 8, 1, 2, 3, 400, DateTimeKind.Utc), fromUtc);
    }
}
