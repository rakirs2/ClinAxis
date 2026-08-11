using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class IncrementalWindowPlannerTests
{
    [TestMethod]
    public void NextWindowEnd_NoPreviousSync_UsesCurrentTime()
    {
        var now = new DateTime(2026, 8, 11, 16, 0, 0, DateTimeKind.Utc);

        Assert.AreEqual(now, IncrementalWindowPlanner.NextWindowEnd(null, now));
    }

    [TestMethod]
    public void NextWindowEnd_StaleCursor_CapsWindowAt24Hours()
    {
        var lastSync = new DateTime(2026, 8, 9, 21, 10, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 11, 16, 0, 0, DateTimeKind.Utc);

        Assert.AreEqual(lastSync.AddDays(1), IncrementalWindowPlanner.NextWindowEnd(lastSync, now));
    }

    [TestMethod]
    public void NextWindowEnd_RecentCursor_UsesCurrentTime()
    {
        var lastSync = new DateTime(2026, 8, 11, 15, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 11, 16, 0, 0, DateTimeKind.Utc);

        Assert.AreEqual(now, IncrementalWindowPlanner.NextWindowEnd(lastSync, now));
    }

    [TestMethod]
    public void NextWindowEnd_CurrentTimeBeforeCursor_Throws()
    {
        var lastSync = new DateTime(2026, 8, 11, 16, 0, 0, DateTimeKind.Utc);
        var now = lastSync.AddMinutes(-1);

        Assert.Throws<ArgumentOutOfRangeException>(() => IncrementalWindowPlanner.NextWindowEnd(lastSync, now));
    }
}
