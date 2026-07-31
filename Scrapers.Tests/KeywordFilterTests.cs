using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class KeywordFilterTests
{
    [TestMethod]
    public void Filter_FullFixture_MirrorsIntegrationExpectations()
    {
        var (cleaned, rejected) = KeywordFilter.Filter(
            [
                "cancer", "cancer",
                "lung cancer",
                "l",
                "HIV",
                "A very long keyword over one hundred fifty characters that should be rejected by the filter because it is way too long for a keyword",
                "treatment",
                "healthy subjects",
                "safety",
                "diabetes; obesity",
                "clinical trial",
                "cancer, ",
                "Parkinson's disease",
                "EXERCISE",
                "a|b|c",
                "stroke, hippotherapy, balance, postural control, gait",
                "diagnosis",
                "therapy",
            ],
            conditions: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cancer" });

        Assert.AreEqual(4, cleaned.Count, "Only LUNG CANCER, HIV, PARKINSON'S DISEASE, EXERCISE should remain");
        CollectionAssert.Contains(cleaned, "LUNG CANCER");
        CollectionAssert.Contains(cleaned, "HIV");
        CollectionAssert.Contains(cleaned, "PARKINSON'S DISEASE");
        CollectionAssert.Contains(cleaned, "EXERCISE");

        Assert.IsFalse(rejected.Contains("CANCER"), "Condition-duplicate keyword should not be rejected");
        CollectionAssert.Contains(rejected, "TREATMENT");
        CollectionAssert.Contains(rejected, "SAFETY");
        CollectionAssert.Contains(rejected, "DIAGNOSIS");
        CollectionAssert.Contains(rejected, "THERAPY");
        CollectionAssert.Contains(rejected, "HEALTHY SUBJECTS");
        CollectionAssert.Contains(rejected, "CLINICAL TRIAL");
    }

    [TestMethod]
    public void Filter_TooShortKeyword_NotMedicalTerm_IsRejected()
    {
        var (cleaned, rejected) = KeywordFilter.Filter(["l"], null);

        Assert.AreEqual(0, cleaned.Count);
        CollectionAssert.Contains(rejected, "L");
    }

    [TestMethod]
    public void Filter_ShortMedicalTerm_IsKept()
    {
        var (cleaned, _) = KeywordFilter.Filter(["HIV"], null);

        CollectionAssert.Contains(cleaned, "HIV");
    }

    [TestMethod]
    public void Filter_SemicolonPipeAndManyCommas_AreRejected()
    {
        var (cleaned, rejected) = KeywordFilter.Filter(
            ["diabetes; obesity", "a|b|c", "stroke, hippotherapy, balance, postural control, gait"],
            null);

        Assert.AreEqual(0, cleaned.Count);
        Assert.AreEqual(3, rejected.Count);
    }

    [TestMethod]
    public void Filter_OverlongKeyword_IsRejected()
    {
        var longKeyword = new string('x', 160);
        var (cleaned, rejected) = KeywordFilter.Filter([longKeyword], null);

        Assert.AreEqual(0, cleaned.Count);
        Assert.AreEqual(1, rejected.Count);
    }

    [TestMethod]
    public void Filter_TrailingPunctuation_NormalizedBeforeBlocklist()
    {
        var (cleaned, _) = KeywordFilter.Filter(["cancer, ", "exercise!"], null);

        CollectionAssert.Contains(cleaned, "CANCER");
        CollectionAssert.Contains(cleaned, "EXERCISE");
    }

    [TestMethod]
    public void Filter_BlocklistMatch_IsCaseInsensitive()
    {
        var (cleaned, rejected) = KeywordFilter.Filter(["Treatment", "SAFETY"], null);

        Assert.AreEqual(0, cleaned.Count);
        Assert.AreEqual(2, rejected.Count);
    }

    [TestMethod]
    public void Filter_NullConditions_OnlyBlocklistAndLengthRulesApply()
    {
        var (cleaned, _) = KeywordFilter.Filter(["cancer", "HIV", "lung cancer"], null);

        Assert.AreEqual(3, cleaned.Count);
    }

    [TestMethod]
    public void Filter_EmptyAndWhitespaceKeywords_AreDroppedSilently()
    {
        var (cleaned, rejected) = KeywordFilter.Filter(["", "   "], null);

        Assert.AreEqual(0, cleaned.Count);
        Assert.AreEqual(0, rejected.Count);
    }
}
