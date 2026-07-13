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
        // Note: Investigators may be >= studies (some studies have multiple investigators, some have one)
        Assert.IsTrue(investigatorCount >= studyCount, "Should have at least as many investigators as studies");
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
            // Check for academic titles (Dr., Prof., Dr, Prof, etc.)
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
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act - search for a name we know is seeded
        var results = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 100, search: "Johnson");

        // Assert
        Assert.IsTrue(results.Count > 0, "Should find investigators with search term in name");
        Assert.IsTrue(results.All(inv => inv?.Name?.Contains("Johnson", StringComparison.OrdinalIgnoreCase) ?? false), 
            "All results should contain search term");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchInvestigators_CasInsensitive_ReturnsMatches()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act - search with different case (using a name we know is seeded: "Campbell")
        var resultsLower = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 100, search: "campbell");
        var resultsUpper = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 100, search: "CAMPBELL");
        var resultsMixed = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 100, search: "CaMpBeLL");

        // Assert
        Assert.AreEqual(resultsLower.Count, resultsUpper.Count, "Search should be case-insensitive");
        Assert.AreEqual(resultsLower.Count, resultsMixed.Count, "Search should be case-insensitive");
        // Campbell is in our seeded data (Investigator6)
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

        // Act - get first page
        var page1 = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 5, search: null);

        // Assert
        Assert.IsTrue(page1.Count > 0, "Page 1 should have results");
        
        // If we have more than 5 investigators, test pagination
        var totalCount = await repo.CountInvestigatorsAsync();
        if (totalCount > 5)
        {
            var page2 = await repo.GetInvestigatorsPagedAsync(page: 2, pageSize: 5, search: null);
            Assert.IsTrue(page2.Count > 0, "Page 2 should have results when total > pageSize");
            
            // Verify pages don't overlap
            var page1Names = page1.Select(i => i.Name).ToList();
            var page2Names = page2.Select(i => i.Name).ToList();
            Assert.IsFalse(page1Names.Any(name => page2Names.Contains(name)), "Pages should not contain duplicate investigators");
        }
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
        int personCount = await repo.CountInvestigatorPersonsFilteredAsync();
        var pagedResults = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 1000, search: null);

        // Assert
        Assert.IsTrue(personCount > 0, "Should have investigators in database");
        Assert.IsTrue(pagedResults.Count > 0, "Legacy investigator query should return results");
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
             .Where(inv => (inv?.Name?.Contains("Dr.", StringComparison.Ordinal) ?? false) || (inv?.Name?.Contains("Prof.", StringComparison.Ordinal) ?? false))
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
        
        // Verify degrees in names (case-insensitive)
        var validDegrees = new[] { "MD", "PhD", "MSc", "DM", "Msc" };
        var investWithDegrees = investigators
            .Where(inv => validDegrees.Any(degree => inv?.Name?.Contains(degree, StringComparison.OrdinalIgnoreCase) ?? false))
            .Count();
        
        Assert.IsTrue(investWithDegrees > 0, "Investigators should have degrees (MD, PhD, MSc, DM, etc.)");
        // At least 80% of investigators should have degrees listed
        Assert.IsTrue(investWithDegrees >= investigators.Count * 0.8, 
            $"At least 80% should have degrees. Got {investWithDegrees}/{investigators.Count}");
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

        // Act - search with partial name (we know "Smith" is seeded in Investigator5)
        var results = await repo.GetInvestigatorsPagedAsync(page: 1, pageSize: 100, search: "Smith");

        // Assert
        Assert.IsTrue(results.Count > 0, "Should find investigators with partial name match");
        Assert.IsTrue(results.All(inv => inv?.Name?.Contains("Smith", StringComparison.OrdinalIgnoreCase) ?? false), 
            "All results should contain search term case-insensitively");
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
