using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class PublicationScrubScopingTests
{
    private static readonly (string StudyNctId, string? Pmid)[] References =
    [
        ("NCT1", "10000001"),
        ("NCT1", "10000002"),
        ("NCT2", "10000003"),
        ("NCT2", null),
        ("NCT2", "  "),
    ];

    [TestMethod]
    public void CandidatePmidsForPersonStudies_IncludesOnlyPersonsOwnStudyPmids()
    {
        var pmids = PublicationScrubScoping.CandidatePmidsForPersonStudies(
            new HashSet<string>(["NCT1"], StringComparer.Ordinal), References);

        CollectionAssert.AreEquivalent(new[] { "10000001", "10000002" }, pmids.ToList());
    }

    [TestMethod]
    public void CandidatePmidsForPersonStudies_MultipleStudies_CombinesPmids()
    {
        var pmids = PublicationScrubScoping.CandidatePmidsForPersonStudies(
            new HashSet<string>(["NCT1", "NCT2"], StringComparer.Ordinal), References);

        CollectionAssert.AreEquivalent(new[] { "10000001", "10000002", "10000003" }, pmids.ToList());
    }

    [TestMethod]
    public void CandidatePmidsForPersonStudies_DeduplicatesPmids()
    {
        (string StudyNctId, string? Pmid)[] references =
        [
            ("NCT1", "10000001"),
            ("NCT2", "10000001"),
        ];

        var pmids = PublicationScrubScoping.CandidatePmidsForPersonStudies(
            new HashSet<string>(["NCT1", "NCT2"], StringComparer.Ordinal), references);

        Assert.AreEqual(1, pmids.Count);
        Assert.AreEqual("10000001", pmids[0]);
    }

    [TestMethod]
    public void CandidatePmidsForPersonStudies_EmptyPersonStudies_ReturnsEmpty()
    {
        var pmids = PublicationScrubScoping.CandidatePmidsForPersonStudies(
            new HashSet<string>(StringComparer.Ordinal), References);

        Assert.AreEqual(0, pmids.Count);
    }

    [TestMethod]
    public void CandidatePmidsForPersonStudies_NoMatchingReferences_ReturnsEmpty()
    {
        var pmids = PublicationScrubScoping.CandidatePmidsForPersonStudies(
            new HashSet<string>(["NCT999"], StringComparer.Ordinal), References);

        Assert.AreEqual(0, pmids.Count);
    }
}
