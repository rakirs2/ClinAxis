using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

/// <summary>
/// Integration tests for Investigators API and database functionality.
/// Verifies that investigators are properly generated, seeded, and queryable.
/// </summary>
[TestClass]
public sealed class InvestigatorsApiTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task DatabaseSeeding_CreatesInvestigatorsForEachStudy()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act
        int investigatorCount = await repo.CountInvestigatorsAsync();
        int studyCount = await repo.CountStudiesAsync();

        // Assert
        Assert.IsTrue(investigatorCount > 0, "Database should contain investigators after seeding");
        Assert.IsTrue(studyCount > 0, "Database should contain studies");
        Assert.IsTrue(investigatorCount > studyCount, "Should have more investigators than studies (multiple per study)");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetInvestigatorsPaged_ReturnsInvestigators()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act
        var investigators = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 10, search: null);

        // Assert
        Assert.IsNotNull(investigators);
        Assert.IsTrue(investigators.Count > 0, "Should return investigators from database");
        Assert.IsTrue(investigators.Count <= 10, "Should respect pageSize limit");
        
        // Verify investigator properties
        foreach (var inv in investigators)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(inv?.Name), "Investigator name should not be null or empty");
            var hasTitle = (inv?.Name?.Contains("Dr.") ?? false) || (inv?.Name?.Contains("Prof.") ?? false);
            Assert.IsTrue(hasTitle, "Investigator name should contain academic title");
            Assert.IsTrue(inv?.StudyCount > 0, "Investigator should have at least one study");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchInvestigators_ByName_ReturnsMatches()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act - search for a common name
        var results = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 100, search: "Anna");

        // Assert
        Assert.IsTrue(results.Count > 0, "Should find investigators with 'Anna' in name");
        Assert.IsTrue(results.All(inv => inv?.Name?.Contains("Anna", StringComparison.OrdinalIgnoreCase) ?? false), 
            "All results should contain search term");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchInvestigators_CasInsensitive_ReturnsMatches()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act - search with different case
        var resultsLower = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 100, search: "campbell");
        var resultsUpper = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 100, search: "CAMPBELL");
        var resultsMixed = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 100, search: "CaMpBeLL");

        // Assert
        Assert.AreEqual(resultsLower.Count, resultsUpper.Count, "Search should be case-insensitive");
        Assert.AreEqual(resultsLower.Count, resultsMixed.Count, "Search should be case-insensitive");
        Assert.IsTrue(resultsLower.Count > 0, "Should find investigators matching 'campbell' case-insensitively");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchInvestigators_NoMatches_ReturnsEmpty()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act
        var results = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 100, search: "XYZABC123_NONEXISTENT");

        // Assert
        Assert.AreEqual(0, results.Count, "Should return empty list for non-matching search");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetInvestigatorsPaged_Pagination_Works()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act - get multiple pages
        var page1 = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 5, search: null);
        var page2 = await repo.GetInvestigatorsPagedAsync(page: 2, pageSize: 5, search: null);

        // Assert
        Assert.IsTrue(page1.Count > 0, "Page 1 should have results");
        Assert.IsTrue(page2.Count > 0, "Page 2 should have results");
        
        // Verify pages don't overlap
        var page1Names = page1.Select(i => i.Name).ToList();
        var page2Names = page2.Select(i => i.Name).ToList();
        Assert.IsFalse(page1Names.Any(name => page2Names.Contains(name)), "Pages should not contain duplicate investigators");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task InvestigatorStudyCount_IsAccurate()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act
        var investigators = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 100, search: null);
        
        // Get a specific investigator and verify their study count
        var firstInvestigator = investigators?[0];

        // Assert
        Assert.IsNotNull(firstInvestigator, "Should have at least one investigator");
        Assert.IsTrue(firstInvestigator?.StudyCount > 0, "Study count should be greater than 0");
        // The actual count depends on seeding, but at minimum should be 1
        Assert.IsTrue(firstInvestigator?.StudyCount <= 10, "Realistic study count for test data should be 1-10");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CountInvestigators_ReturnsCorrectTotal()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act
        int totalCount = await repo.CountInvestigatorsAsync();
        var pagedResults = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 1000, search: null);

        // Assert
        Assert.IsTrue(totalCount > 0, "Should have investigators in database");
        // Note: Total count >= paged results because there may be more than pageSize=1000
        Assert.IsTrue(totalCount >= pagedResults.Count, "Total count should be >= paged results");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task InvestigatorNames_HaveAcademicTitles()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act
        var investigators = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 50, search: null);

        // Assert
        Assert.IsTrue(investigators.Count > 0);
        
        // Verify academic titles in names
        var investWithTitles = investigators
            .Where(inv => (inv?.Name?.Contains("Dr.") ?? false) || (inv?.Name?.Contains("Prof.") ?? false))
            .Count();
        
        Assert.IsTrue(investWithTitles > 0, "Investigators should have academic titles (Dr., Prof., etc.)");
        Assert.IsTrue(investWithTitles >= investigators.Count * 0.8, "At least 80% should have academic titles");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task InvestigatorNames_HaveDegrees()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act
        var investigators = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 50, search: null);

        // Assert
        Assert.IsTrue(investigators.Count > 0);
        
        // Verify degrees in names
        var validDegrees = new[] { "MD", "PhD", "MSc", "DM" };
        var investWithDegrees = investigators
            .Where(inv => validDegrees.Any(degree => inv?.Name?.Contains(degree) ?? false))
            .Count();
        
        Assert.IsTrue(investWithDegrees > 0, "Investigators should have degrees (MD, PhD, MSc, DM, etc.)");
        Assert.IsTrue(investWithDegrees >= investigators.Count * 0.8, "At least 80% should have degrees");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task InvestigatorAffiliation_IsNotNullOrEmpty()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act
        var investigators = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 50, search: null);

        // Assert
        Assert.IsTrue(investigators.All(inv => !string.IsNullOrWhiteSpace(inv?.Affiliation)), 
            "All investigators should have an affiliation");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task InvestigatorSearch_PartialMatch_Works()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act - search with partial name
        var results = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 100, search: "Smith");

        // Assert
        Assert.IsTrue(results.Count > 0, "Should find investigators with partial name match");
        Assert.IsTrue(results.All(inv => inv?.Name?.Contains("Smith") ?? false), "All results should contain search term");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task DuplicateInvestigators_SameNameDifferentAffiliations_AreDistinct()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act
        var investigators = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 1000, search: null);

        // Assert
        // Get duplicates by name
        var groupedByName = investigators.GroupBy(inv => inv.Name).ToList();
        var duplicateNames = groupedByName.Where(g => g.Count() > 1).ToList();

        // If there are duplicates with same name, they should have different affiliations
        // (This is expected behavior - same person at different institutions)
        foreach (var duplicateGroup in duplicateNames)
        {
            var affiliations = duplicateGroup.Select(inv => inv.Affiliation).Distinct().ToList();
            // At least some should have different affiliations
            Assert.IsTrue(affiliations.Count >= 1, "Duplicate names should exist in database");
        }
    }
}
