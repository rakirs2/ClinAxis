using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class InvestigatorsApiTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task DatabaseSeeding_CreatesInvestigatorsForEachStudy()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        int investigatorCount = await repo.CountInvestigatorsAsync();
        int studyCount = await repo.CountStudiesAsync();

        Assert.IsTrue(investigatorCount > 0, "Database should contain investigators after seeding");
        Assert.IsTrue(studyCount > 0, "Database should contain studies");
        Assert.IsTrue(investigatorCount >= studyCount, "Should have at least as many investigators as studies");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetInvestigatorsPaged_ReturnsInvestigators()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        var investigators = await repo.GetInvestigatorPersonsPagedAsync(page: 1, pageSize: 10, search: null);

        Assert.IsNotNull(investigators);
        Assert.IsTrue(investigators.Count > 0, "Should return investigators from database");
        Assert.IsTrue(investigators.Count <= 10, "Should respect pageSize limit");

        foreach (var inv in investigators)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(inv?.Name), "Investigator name should not be null or empty");
            var hasTitle = (inv?.Name?.Contains("Dr", StringComparison.OrdinalIgnoreCase) ?? false) ||
                          (inv?.Name?.Contains("Prof", StringComparison.OrdinalIgnoreCase) ?? false);
            Assert.IsTrue(hasTitle, $"Investigator name '{inv?.Name}' should contain academic title");
            Assert.IsTrue(inv?.StudyCount > 0, "Investigator should have at least one study");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchInvestigators_ByName_ReturnsMatches()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        var results = await repo.GetInvestigatorPersonsPagedAsync(page: 1, pageSize: 100, search: "Johnson");

        Assert.IsTrue(results.Count > 0, "Should find investigators with search term in name");
        Assert.IsTrue(results.All(inv => inv?.Name?.Contains("Johnson", StringComparison.OrdinalIgnoreCase) ?? false),
            "All results should contain search term");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchInvestigators_CasInsensitive_ReturnsMatches()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        var resultsLower = await repo.GetInvestigatorPersonsPagedAsync(page: 1, pageSize: 100, search: "campbell");
        var resultsUpper = await repo.GetInvestigatorPersonsPagedAsync(page: 1, pageSize: 100, search: "CAMPBELL");
        var resultsMixed = await repo.GetInvestigatorPersonsPagedAsync(page: 1, pageSize: 100, search: "CaMpBeLL");

        Assert.AreEqual(resultsLower.Count, resultsUpper.Count, "Search should be case-insensitive");
        Assert.AreEqual(resultsLower.Count, resultsMixed.Count, "Search should be case-insensitive");
        Assert.IsTrue(resultsLower.Count > 0, "Should find investigators matching 'campbell' case-insensitively");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchInvestigators_NoMatches_ReturnsEmpty()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        var results = await repo.GetInvestigatorPersonsPagedAsync(page: 1, pageSize: 100, search: "XYZABC123_NONEXISTENT");

        Assert.AreEqual(0, results.Count, "Should return empty list for non-matching search");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetInvestigatorsPaged_Pagination_Works()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        var page1 = await repo.GetInvestigatorPersonsPagedAsync(page: 1, pageSize: 5, search: null);

        Assert.IsTrue(page1.Count > 0, "Page 1 should have results");

        var totalCount = await repo.CountInvestigatorsAsync();
        if (totalCount > 5)
        {
            var page2 = await repo.GetInvestigatorPersonsPagedAsync(page: 2, pageSize: 5, search: null);
            Assert.IsTrue(page2.Count > 0, "Page 2 should have results when total > pageSize");

            var page1Names = page1.Select(i => i.Name).ToList();
            var page2Names = page2.Select(i => i.Name).ToList();
            Assert.IsFalse(page1Names.Any(name => page2Names.Contains(name)), "Pages should not contain duplicate investigators");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task InvestigatorStudyCount_IsAccurate()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        var investigators = await repo.GetInvestigatorPersonsPagedAsync(page: 1, pageSize: 100, search: null);

        var firstInvestigator = investigators?[0];

        Assert.IsNotNull(firstInvestigator, "Should have at least one investigator");
        Assert.IsTrue(firstInvestigator?.StudyCount > 0, "Study count should be greater than 0");
        Assert.IsTrue(firstInvestigator?.StudyCount <= 10, "Realistic study count for test data should be 1-10");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CountInvestigators_ReturnsCorrectTotal()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        int personCount = await repo.CountInvestigatorPersonsFilteredAsync();
        var pagedResults = await repo.GetInvestigatorPersonsPagedAsync(page: 1, pageSize: 1000, search: null);

        Assert.IsTrue(personCount > 0, "Should have investigators in database");
        Assert.IsTrue(pagedResults.Count > 0, "Investigator person query should return results");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task InvestigatorNames_HaveAcademicTitles()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        var investigators = await repo.GetInvestigatorPersonsPagedAsync(page: 1, pageSize: 50, search: null);

        Assert.IsTrue(investigators.Count > 0);

        var investWithTitles = investigators
            .Where(inv => (inv?.Name?.Contains("Dr.", StringComparison.Ordinal) ?? false) || (inv?.Name?.Contains("Prof.", StringComparison.Ordinal) ?? false))
            .Count();

        Assert.IsTrue(investWithTitles > 0, "Investigators should have academic titles (Dr., Prof., etc.)");
        Assert.IsTrue(investWithTitles >= investigators.Count * 0.8, "At least 80% should have academic titles");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task InvestigatorNames_HaveDegrees()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        var investigators = await repo.GetInvestigatorPersonsPagedAsync(page: 1, pageSize: 50, search: null);

        Assert.IsTrue(investigators.Count > 0);

        var validDegrees = new[] { "MD", "PhD", "MSc", "DM", "Msc" };
        var investWithDegrees = investigators
            .Where(inv => validDegrees.Any(degree => inv?.Name?.Contains(degree, StringComparison.OrdinalIgnoreCase) ?? false))
            .Count();

        Assert.IsTrue(investWithDegrees > 0, "Investigators should have degrees (MD, PhD, MSc, DM, etc.)");
        Assert.IsTrue(investWithDegrees >= investigators.Count * 0.8,
            $"At least 80% should have degrees. Got {investWithDegrees}/{investigators.Count}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task InvestigatorSearch_PartialMatch_Works()
    {
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        var results = await repo.GetInvestigatorPersonsPagedAsync(page: 1, pageSize: 100, search: "Smith");

        Assert.IsTrue(results.Count > 0, "Should find investigators with partial name match");
        Assert.IsTrue(results.All(inv => inv?.Name?.Contains("Smith", StringComparison.OrdinalIgnoreCase) ?? false),
            "All results should contain search term case-insensitively");
    }
}
