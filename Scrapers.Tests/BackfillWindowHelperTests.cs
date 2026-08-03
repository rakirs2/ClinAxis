using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class BackfillWindowHelperTests
{
    private static readonly (DateOnly From, DateOnly To) July = (new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

    [TestMethod]
    public void IsCovered_FullyContainedWindow_ReturnsTrue()
    {
        (DateOnly, DateOnly)[] completed = [July];

        Assert.IsTrue(BackfillWindowHelper.IsCovered((new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 20)), completed));
        Assert.IsTrue(BackfillWindowHelper.IsCovered((new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)), completed));
    }

    [TestMethod]
    public void IsCovered_PartialOverlap_ReturnsFalse()
    {
        (DateOnly, DateOnly)[] completed = [July];

        Assert.IsFalse(BackfillWindowHelper.IsCovered((new DateOnly(2026, 6, 25), new DateOnly(2026, 7, 5)), completed));
        Assert.IsFalse(BackfillWindowHelper.IsCovered((new DateOnly(2026, 7, 20), new DateOnly(2026, 8, 5)), completed));
    }

    [TestMethod]
    public void IsCovered_DisjointWindows_ReturnsFalse()
    {
        (DateOnly, DateOnly)[] completed = [July];

        Assert.IsFalse(BackfillWindowHelper.IsCovered((new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), completed));
    }

    [TestMethod]
    public void IsCovered_MultipleCompletedWindows_MatchesAny()
    {
        (DateOnly, DateOnly)[] completed =
        [
            (new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15)),
            (new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31))
        ];

        Assert.IsTrue(BackfillWindowHelper.IsCovered((new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20)), completed));
        Assert.IsFalse(BackfillWindowHelper.IsCovered((new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 25)), completed));
    }

    [TestMethod]
    public void IsCovered_NullCompletedWindows_Throws()
    {
        IEnumerable<(DateOnly, DateOnly)>? completed = null;
        Assert.Throws<ArgumentNullException>(() => BackfillWindowHelper.IsCovered((new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)), completed!));
    }
}
