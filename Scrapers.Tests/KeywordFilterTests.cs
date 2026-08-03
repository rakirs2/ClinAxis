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

        Assert.AreEqual(5, cleaned.Count, "Only LUNG CANCER, HUMAN IMMUNODEFICIENCY VIRUS, PARKINSON'S DISEASE, EXERCISE, CLINICAL TRIAL should remain");
        CollectionAssert.Contains(cleaned, "LUNG CANCER");
        CollectionAssert.Contains(cleaned, "HUMAN IMMUNODEFICIENCY VIRUS");
        CollectionAssert.Contains(cleaned, "PARKINSON'S DISEASE");
        CollectionAssert.Contains(cleaned, "EXERCISE");
        CollectionAssert.Contains(cleaned, "CLINICAL TRIAL");

        Assert.IsFalse(rejected.Contains("CANCER"), "Condition-duplicate keyword should not be rejected");
        CollectionAssert.Contains(rejected, "TREATMENT");
        CollectionAssert.Contains(rejected, "SAFETY");
        CollectionAssert.Contains(rejected, "DIAGNOSIS");
        CollectionAssert.Contains(rejected, "THERAPY");
        CollectionAssert.Contains(rejected, "HEALTHY SUBJECTS");
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

        CollectionAssert.Contains(cleaned, "HUMAN IMMUNODEFICIENCY VIRUS");
    }

    [TestMethod]
    public void Filter_Acronym_IsExpandedToFullTerm()
    {
        var (cleaned, rejected) = KeywordFilter.Filter(["MI", "CVA", "DKA", "PE", "DVT", "ARDS", "MRSA", "AF", "HF"], null);

        Assert.AreEqual(9, cleaned.Count);
        CollectionAssert.Contains(cleaned, "MYOCARDIAL INFARCTION");
        CollectionAssert.Contains(cleaned, "CEREBROVASCULAR ACCIDENT");
        CollectionAssert.Contains(cleaned, "DIABETIC KETOACIDOSIS");
        CollectionAssert.Contains(cleaned, "PULMONARY EMBOLISM");
        CollectionAssert.Contains(cleaned, "DEEP VEIN THROMBOSIS");
        CollectionAssert.Contains(cleaned, "ACUTE RESPIRATORY DISTRESS SYNDROME");
        CollectionAssert.Contains(cleaned, "METHICILLIN-RESISTANT STAPHYLOCOCCUS AUREUS");
        CollectionAssert.Contains(cleaned, "ATRIAL FIBRILLATION");
        CollectionAssert.Contains(cleaned, "HEART FAILURE");
        Assert.AreEqual(0, rejected.Count);
    }

    [TestMethod]
    public void ExpandAcronym_IsCaseInsensitive()
    {
        Assert.AreEqual("myocardial infarction", KeywordFilter.ExpandAcronym("MI"));
        Assert.AreEqual("myocardial infarction", KeywordFilter.ExpandAcronym("mi"));
        Assert.AreEqual("myocardial infarction", KeywordFilter.ExpandAcronym("  MI  "));
    }

    [TestMethod]
    public void ExpandAcronym_NonAcronym_IsUnchanged()
    {
        Assert.AreEqual("cancer", KeywordFilter.ExpandAcronym("cancer"));
        Assert.AreEqual("lung cancer", KeywordFilter.ExpandAcronym("lung cancer"));
        Assert.AreEqual("", KeywordFilter.ExpandAcronym(""));
    }

    [TestMethod]
    public void ExpandAcronym_MultiWordKeyword_IsUnchanged()
    {
        Assert.AreEqual("MI protocol", KeywordFilter.ExpandAcronym("MI protocol"));
    }

    [TestMethod]
    public void Filter_UnknownShortAcronym_IsStillRejected()
    {
        var (cleaned, rejected) = KeywordFilter.Filter(["XZ"], null);

        Assert.AreEqual(0, cleaned.Count);
        CollectionAssert.Contains(rejected, "XZ");
    }

    [TestMethod]
    public void Filter_ExpandedAcronym_DeduplicatesAgainstFullTerm()
    {
        var (cleaned, rejected) = KeywordFilter.Filter(["MI", "myocardial infarction"], null);

        Assert.AreEqual(1, cleaned.Count);
        CollectionAssert.Contains(cleaned, "MYOCARDIAL INFARCTION");
        Assert.AreEqual(0, rejected.Count);
    }

    [TestMethod]
    public void Filter_ExpandedAcronym_DroppedWhenConditionAlreadyCoversIt()
    {
        var (cleaned, rejected) = KeywordFilter.Filter(
            ["MI"],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "myocardial infarction" });

        Assert.AreEqual(0, cleaned.Count, "Expanded term is a condition duplicate");
        Assert.AreEqual(0, rejected.Count, "Condition-duplicate keywords are not rejected");
    }

    [TestMethod]
    public void Filter_ShortListFallback_StillKeepsNonExpandedTerms()
    {
        var (cleaned, _) = KeywordFilter.Filter(["ICU"], null);

        CollectionAssert.Contains(cleaned, "ICU");
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

    [TestMethod]
    public void Filter_DesignDescriptors_AreKeptDespiteBlocklist()
    {
        var (cleaned, rejected) = KeywordFilter.Filter(
            ["Randomised Controlled Trial", "pilot study", "open label", "phase 3", "cohort study", "case-control", "crossover study"],
            null);

        Assert.AreEqual(7, cleaned.Count);
        CollectionAssert.Contains(cleaned, "RANDOMISED CONTROLLED TRIAL");
        CollectionAssert.Contains(cleaned, "PILOT STUDY");
        CollectionAssert.Contains(cleaned, "OPEN LABEL");
        CollectionAssert.Contains(cleaned, "PHASE 3");
        CollectionAssert.Contains(cleaned, "COHORT STUDY");
        CollectionAssert.Contains(cleaned, "CASE-CONTROL");
        CollectionAssert.Contains(cleaned, "CROSSOVER STUDY");
        Assert.AreEqual(0, rejected.Count);
    }

    [TestMethod]
    public void Filter_GenericJunk_StillRejected()
    {
        var (cleaned, rejected) = KeywordFilter.Filter(
            ["safety", "treatment", "efficacy", "outcomes", "patient", "multicenter"],
            null);

        Assert.AreEqual(0, cleaned.Count);
        Assert.AreEqual(6, rejected.Count);
    }

    [TestMethod]
    public void Normalize_TrimsTrailingPunctuationAndUpperCases()
    {
        Assert.AreEqual("CANCER", KeywordFilter.Normalize("  cancer, "));
        Assert.AreEqual("EXERCISE", KeywordFilter.Normalize("exercise!"));
        Assert.AreEqual("TYPE 2 DIABETES", KeywordFilter.Normalize("  type 2 diabetes "));
    }

    [TestMethod]
    public void ApplyMeSHGate_MovesMatchedRejectedKeywordsToAccepted()
    {
        var (accepted, rejected) = KeywordFilter.ApplyMeSHGate(
            rejected: ["CVA", "DIABETES"],
            meshMatchedKeywords: ["DIABETES"]);

        CollectionAssert.Contains(accepted, "DIABETES");
        Assert.AreEqual(1, rejected.Count);
        CollectionAssert.Contains(rejected, "CVA");
    }

    [TestMethod]
    public void ApplyMeSHGate_IsCaseInsensitive()
    {
        var (accepted, rejected) = KeywordFilter.ApplyMeSHGate(
            rejected: ["diabetes"],
            meshMatchedKeywords: ["DIABETES"]);

        Assert.AreEqual(1, accepted.Count);
        Assert.AreEqual(0, rejected.Count);
    }

    [TestMethod]
    public void ApplyMeSHGate_NoMatches_KeepsAllRejected()
    {
        var (accepted, rejected) = KeywordFilter.ApplyMeSHGate(
            rejected: ["SAFETY", "TREATMENT"],
            meshMatchedKeywords: []);

        Assert.AreEqual(0, accepted.Count);
        Assert.AreEqual(2, rejected.Count);
    }

    [TestMethod]
    public void IsJunkBlocked_OnlyGenericJunkNotAllowlisted()
    {
        Assert.IsTrue(KeywordFilter.IsJunkBlocked("TREATMENT"));
        Assert.IsTrue(KeywordFilter.IsJunkBlocked("safety"));
        Assert.IsTrue(KeywordFilter.IsJunkBlocked("efficacy"));

        Assert.IsFalse(KeywordFilter.IsJunkBlocked("PILOT STUDY"), "Design descriptors bypass the blocklist");
        Assert.IsFalse(KeywordFilter.IsJunkBlocked("RANDOMISED CONTROLLED TRIAL"));
        Assert.IsFalse(KeywordFilter.IsJunkBlocked("CANCER"), "Non-blocklisted keywords are not junk");
    }
}
