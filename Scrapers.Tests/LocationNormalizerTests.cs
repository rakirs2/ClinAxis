using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class LocationNormalizerTests
{
    [TestMethod]
    public void NormalizeCountry_UsAliases_AllMapToUnitedStates()
    {
        foreach (var alias in new[] { "USA", "US", "U.S.", "U.S.A", "U.S.A.", "United States of America", "America", "  usa " })
        {
            Assert.AreEqual("United States", LocationNormalizer.NormalizeCountry(alias), $"alias '{alias}' must map to canonical form");
        }
    }

    [TestMethod]
    public void NormalizeCountry_UkAliases_AllMapToUnitedKingdom()
    {
        foreach (var alias in new[] { "UK", "U.K.", "GB", "Great Britain", "Britain" })
        {
            Assert.AreEqual("United Kingdom", LocationNormalizer.NormalizeCountry(alias), $"alias '{alias}' must map to canonical form");
        }
    }

    [TestMethod]
    public void NormalizeCountry_Unknown_TrimmedAndCasePreserved()
    {
        Assert.AreEqual("Germany", LocationNormalizer.NormalizeCountry("  Germany "));
    }

    [TestMethod]
    public void NormalizeCountry_NullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(LocationNormalizer.NormalizeCountry(null));
        Assert.IsNull(LocationNormalizer.NormalizeCountry(""));
        Assert.IsNull(LocationNormalizer.NormalizeCountry("   "));
    }

    [TestMethod]
    public void NormalizeState_FullName_BecomesTwoLetterCode()
    {
        Assert.AreEqual("NY", LocationNormalizer.NormalizeState("New York"));
        Assert.AreEqual("CA", LocationNormalizer.NormalizeState("california"));
        Assert.AreEqual("DC", LocationNormalizer.NormalizeState("District of Columbia"));
        Assert.AreEqual("WA", LocationNormalizer.NormalizeState(" Washington "));
    }

    [TestMethod]
    public void NormalizeState_Code_Uppercased()
    {
        Assert.AreEqual("NY", LocationNormalizer.NormalizeState("ny"));
        Assert.AreEqual("MA", LocationNormalizer.NormalizeState("ma"));
    }

    [TestMethod]
    public void NormalizeState_NonUsValue_TrimmedButNotMangled()
    {
        Assert.AreEqual("Ontario", LocationNormalizer.NormalizeState(" Ontario "));
    }

    [TestMethod]
    public void NormalizeState_NullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(LocationNormalizer.NormalizeState(null));
        Assert.IsNull(LocationNormalizer.NormalizeState(""));
    }

    [TestMethod]
    public void NormalizeCity_TrimsAndCollapsesWhitespace()
    {
        Assert.AreEqual("New York", LocationNormalizer.NormalizeCity("  New  York "));
        Assert.AreEqual("San Juan", LocationNormalizer.NormalizeCity("San  Juan"));
    }

    [TestMethod]
    public void NormalizeFacility_TrimsAndCollapsesWhitespace()
    {
        Assert.AreEqual("Johns Hopkins Hospital", LocationNormalizer.NormalizeFacility("  Johns  Hopkins   Hospital "));
    }
}
