using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class PageViewStatsAggregatorTests
{
    private static PageViewEntity View(string path, string sessionId)
    {
        return new PageViewEntity
        {
            Path = path,
            SessionId = sessionId,
            ViewedAt = DateTime.UtcNow
        };
    }

    [TestMethod]
    public void Aggregate_EmptyRows_ReturnsZeroTotals()
    {
        var stats = PageViewStatsAggregator.Aggregate([]);

        Assert.AreEqual(0, stats.TotalViews);
        Assert.AreEqual(0, stats.UniqueVisitors);
        Assert.AreEqual(0, stats.TopPages.Count);
    }

    [TestMethod]
    public void Aggregate_CountsTotalViewsAndUniqueSessions()
    {
        var rows = new List<PageViewEntity>
        {
            View("/status", "session-a"),
            View("/", "session-a"),
            View("/status", "session-a"),
            View("/investigators", "session-b"),
            View("/status", "session-c")
        };

        var stats = PageViewStatsAggregator.Aggregate(rows);

        Assert.AreEqual(5, stats.TotalViews);
        Assert.AreEqual(3, stats.UniqueVisitors);
    }

    [TestMethod]
    public void Aggregate_RanksTopPagesByCountThenPath()
    {
        var rows = new List<PageViewEntity>
        {
            View("/status", "session-a"),
            View("/", "session-a"),
            View("/status", "session-a"),
            View("/investigators", "session-b"),
            View("/status", "session-c")
        };

        var stats = PageViewStatsAggregator.Aggregate(rows);

        Assert.AreEqual("/status", stats.TopPages[0].Path);
        Assert.AreEqual(3, stats.TopPages[0].Count);
        Assert.IsTrue(stats.TopPages[1].Count >= stats.TopPages[2].Count);
    }

    [TestMethod]
    public void Aggregate_TopPages_LimitedToRequestedCount()
    {
        var rows = new List<PageViewEntity>
        {
            View("/a", "session-a"),
            View("/b", "session-b"),
            View("/c", "session-c"),
            View("/d", "session-d")
        };

        var stats = PageViewStatsAggregator.Aggregate(rows, topPages: 2);

        Assert.AreEqual(2, stats.TopPages.Count);
    }

    [TestMethod]
    public void Aggregate_BlankPaths_AreExcludedFromTopPages()
    {
        var rows = new List<PageViewEntity>
        {
            new() { Path = " ", SessionId = "session-a", ViewedAt = DateTime.UtcNow },
            new() { Path = "", SessionId = "session-b", ViewedAt = DateTime.UtcNow },
            new() { Path = "/status", SessionId = "session-c", ViewedAt = DateTime.UtcNow },
            new() { Path = "/status", SessionId = "session-d", ViewedAt = DateTime.UtcNow }
        };

        var stats = PageViewStatsAggregator.Aggregate(rows);

        Assert.AreEqual(4, stats.TotalViews);
        Assert.AreEqual(4, stats.UniqueVisitors);
        Assert.AreEqual("/status", stats.TopPages[0].Path);
        Assert.AreEqual(2, stats.TopPages[0].Count);
        Assert.IsTrue(stats.TopPages.All(p => !string.IsNullOrWhiteSpace(p.Path)));
    }

    [TestMethod]
    public void TryParsePeriod_Day_StartsAtStartOfToday()
    {
        var now = new DateTime(2026, 8, 2, 15, 30, 0, DateTimeKind.Utc);

        Assert.IsTrue(PageViewStatsAggregator.TryParsePeriod("day", now, out var from));

        Assert.AreEqual(new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc), from);
    }

    [TestMethod]
    public void TryParsePeriod_Week_StartsAtMonday()
    {
        // 2 Aug 2026 is a Sunday.
        var now = new DateTime(2026, 8, 2, 15, 30, 0, DateTimeKind.Utc);

        Assert.IsTrue(PageViewStatsAggregator.TryParsePeriod("week", now, out var from));

        Assert.AreEqual(new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc), from);
    }

    [TestMethod]
    public void TryParsePeriod_Month_StartsAtFirstOfMonth()
    {
        var now = new DateTime(2026, 8, 2, 15, 30, 0, DateTimeKind.Utc);

        Assert.IsTrue(PageViewStatsAggregator.TryParsePeriod("month", now, out var from));

        Assert.AreEqual(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), from);
    }

    [TestMethod]
    public void TryParsePeriod_IsCaseInsensitiveAndTrimmed()
    {
        var now = new DateTime(2026, 8, 2, 15, 30, 0, DateTimeKind.Utc);

        Assert.IsTrue(PageViewStatsAggregator.TryParsePeriod("  DAY ", now, out _));
    }

    [TestMethod]
    public void TryParsePeriod_InvalidOrNull_ReturnsFalse()
    {
        var now = DateTime.UtcNow;

        Assert.IsFalse(PageViewStatsAggregator.TryParsePeriod("year", now, out _));
        Assert.IsFalse(PageViewStatsAggregator.TryParsePeriod("", now, out _));
        Assert.IsFalse(PageViewStatsAggregator.TryParsePeriod(null, now, out _));
    }
}