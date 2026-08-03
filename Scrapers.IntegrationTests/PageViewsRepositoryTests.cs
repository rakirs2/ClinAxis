using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;
using Scrapers.Utilities;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class PageViewsRepositoryTests : DbTestBase
{
    private StudyRepository _repo = null!;

    [TestInitialize]
    public void TestInit()
    {
        _repo = new StudyRepository(ConnectionString);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PageViews_PersistAndAggregateWithinWindow()
    {
        var outsideWindow = new PageViewEntity
        {
            Path = "/blog",
            SessionId = "00000000-0000-4000-8000-000000000000",
            ViewedAt = new DateTime(2026, 8, 1, 23, 59, 59, DateTimeKind.Utc)
        };
        Context.PageViews.AddRange(
            outsideWindow,
            new PageViewEntity { Path = "/status", SessionId = "11111111-1111-4111-8111-111111111111", ViewedAt = new DateTime(2026, 8, 2, 8, 0, 0, DateTimeKind.Utc) },
            new PageViewEntity { Path = "/", SessionId = "11111111-1111-4111-8111-111111111111", ViewedAt = new DateTime(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc) },
            new PageViewEntity { Path = "/investigators", SessionId = "22222222-2222-4222-8222-222222222222", ViewedAt = new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc) },
            new PageViewEntity { Path = "/status", SessionId = "33333333-3333-4333-8333-333333333333", ViewedAt = new DateTime(2026, 8, 2, 11, 0, 0, DateTimeKind.Utc) });
        await Context.SaveChangesAsync();

        var persistedId = await _repo.AddPageViewAsync(new PageViewEntity
        {
            Path = "/status",
            SessionId = "11111111-1111-4111-8111-111111111111",
            ViewedAt = new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc)
        });

        var saved = await Context.PageViews.AsNoTracking().SingleAsync(v => v.Id == persistedId);
        Assert.AreEqual("/status", saved.Path);
        Assert.AreEqual("11111111-1111-4111-8111-111111111111", saved.SessionId);
        Assert.AreEqual("2026-08-02 12:00:00", saved.ViewedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

        var rows = await _repo.GetPageViewsAsync(new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc));
        var stats = PageViewStatsAggregator.Aggregate(rows);

        Assert.AreEqual(5, stats.TotalViews, "Rows outside the window must be excluded but persisted");
        Assert.AreEqual(3, stats.UniqueVisitors, "Unique visitors = distinct sessions in window");
        Assert.AreEqual("/status", stats.TopPages[0].Path);
        Assert.AreEqual(3, stats.TopPages[0].Count);
    }
}