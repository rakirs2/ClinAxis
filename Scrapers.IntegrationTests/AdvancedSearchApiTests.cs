using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class AdvancedSearchApiTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetDistinctConditions_ReturnsNonEmptyList()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act
        var conditions = await repo.GetDistinctConditionsAsync();

        // Assert
        Assert.IsNotNull(conditions);
        Assert.IsTrue(conditions.Count > 0, "Should return distinct conditions from seeded data");
        Assert.IsTrue(conditions.All(c => !string.IsNullOrWhiteSpace(c)), "Conditions should not be null or whitespace");
        Assert.AreEqual(conditions.Count, conditions.Distinct().Count(), "Should return only distinct values");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetDistinctLocations_ReturnsAllDimensions()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);

        // Act
        var (countries, states, cities, facilities) = await repo.GetDistinctLocationsAsync();

        // Assert
        Assert.IsNotNull(countries);
        Assert.IsNotNull(states);
        Assert.IsNotNull(cities);
        Assert.IsNotNull(facilities);

        // From seeded data with locations
        Assert.IsTrue(countries.Count > 0, "Should return countries");
        Assert.IsTrue(states.Count > 0, "Should return states");
        Assert.IsTrue(cities.Count > 0, "Should return cities");
        Assert.IsTrue(facilities.Count > 0, "Should return facilities");

        // Verify no nulls or empty strings
        Assert.IsTrue(countries.All(c => !string.IsNullOrWhiteSpace(c)));
        Assert.IsTrue(states.All(s => !string.IsNullOrWhiteSpace(s)));
        Assert.IsTrue(cities.All(c => !string.IsNullOrWhiteSpace(c)));
        Assert.IsTrue(facilities.All(f => !string.IsNullOrWhiteSpace(f)));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWithKeywordOnly_ReturnsMatchingStudies()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);
        var criteria = new StudySearchCriteria { Keyword = "Diabetes" };

        // Act
        var results = await repo.SearchStudiesAsync(criteria);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0, "Should find studies with 'Diabetes' in title or description");
        Assert.IsTrue(results.All(s => s.BriefTitle?.Contains("Diabetes", StringComparison.OrdinalIgnoreCase) ?? false));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWithMultipleStatuses_ReturnsOrLogic()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);
        var criteria = new StudySearchCriteria
        {
            Statuses = new[] { "RECRUITING", "ACTIVE" }
        };

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var total = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0, "Should return RECRUITING or ACTIVE studies");
        Assert.IsTrue(results.All(s => s.OverallStatus == "RECRUITING" || s.OverallStatus == "ACTIVE"));
        Assert.IsTrue(total > 0);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWithConditionFilter_ReturnsMatchingStudies()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);
        var criteria = new StudySearchCriteria
        {
            Conditions = new[] { "Type 2 Diabetes" }
        };

        // Act
        var results = await repo.SearchStudiesAsync(criteria);

        // Assert
        Assert.IsNotNull(results);
        // If condition filter returns results, verify they have the condition
        if (results.Count > 0)
        {
            Assert.IsTrue(results.All(s => 
                s.Conditions?.Any(c => c.Condition == "Type 2 Diabetes") ?? false));
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWithLocationFilter_ReturnsGeoFilteredStudies()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);
        var criteria = new StudySearchCriteria
        {
            Countries = new[] { "United States" }
        };

        // Act
        var results = await repo.SearchStudiesAsync(criteria);

        // Assert
        Assert.IsNotNull(results);
        if (results.Count > 0)
        {
            // Verify all results have at least one location in USA
            foreach (var study in results)
            {
                Assert.IsTrue(
                    study.Locations?.Any(l => l.Country == "United States") ?? false,
                    $"Study {study.NctId} should have US location");
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWithEnrollmentRange_ReturnsStudiesInRange()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);
        var criteria = new StudySearchCriteria
        {
            EnrollmentMin = 100,
            EnrollmentMax = 500
        };

        // Act
        var results = await repo.SearchStudiesAsync(criteria);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.All(s => s.EnrollmentCount >= 100 && s.EnrollmentCount <= 500));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWithDateRange_ReturnsStudiesInRange()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);
        var fromDate = new DateOnly(2024, 1, 1);
        var toDate = new DateOnly(2024, 12, 31);
        var criteria = new StudySearchCriteria
        {
            StartDateFrom = fromDate.ToDateTime(TimeOnly.MinValue),
            StartDateTo = toDate.ToDateTime(TimeOnly.MaxValue)
        };

        // Act
        var results = await repo.SearchStudiesAsync(criteria);

        // Assert
        Assert.IsNotNull(results);
        if (results.Count > 0)
        {
            Assert.IsTrue(results.All(s => 
                s.StartDate >= fromDate && s.StartDate <= toDate));
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWithMultipleFilters_ReturnsCorrectSubset()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);
        var criteria = new StudySearchCriteria
        {
            Statuses = new[] { "RECRUITING" },
            Phases = new[] { "PHASE3" },
            EnrollmentMin = 50
        };

        // Act
        var results = await repo.SearchStudiesAsync(criteria);

        // Assert
        Assert.IsNotNull(results);
        Assert.IsTrue(results.All(s => 
            s.OverallStatus == "RECRUITING" &&
            s.Phases?.Any(p => p.Phase == "PHASE3") == true &&
            s.EnrollmentCount >= 50));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWithNoMatches_ReturnsEmpty()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);
        var criteria = new StudySearchCriteria
        {
            Keyword = "ZZZZZZNOTFOUND"
        };

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var total = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.IsNotNull(results);
        Assert.AreEqual(0, results.Count);
        Assert.AreEqual(0, total);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWithPagination_ReturnsCorrectPage()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);
        var criteria1 = new StudySearchCriteria { Page = 1, PageSize = 5 };
        var criteria2 = new StudySearchCriteria { Page = 2, PageSize = 5 };

        // Act
        var page1 = await repo.SearchStudiesAsync(criteria1);
        var page2 = await repo.SearchStudiesAsync(criteria2);

        // Assert
        Assert.IsNotNull(page1);
        Assert.IsNotNull(page2);
        Assert.IsTrue(page1.Count <= 5);
        Assert.IsTrue(page2.Count <= 5);
        
        // Pages should have different studies (if both have results)
        if (page1.Count > 0 && page2.Count > 0)
        {
            var page1Ids = page1.Select(s => s.NctId).ToList();
            var page2Ids = page2.Select(s => s.NctId).ToList();
            Assert.IsFalse(page1Ids.Intersect(page2Ids).Any(), "Pages should not overlap");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CountStudiesFiltered_MatchesSearchResults()
    {
        // Arrange
        await using var snapshot = new SnapshotDb();
        var repo = new StudyRepository(snapshot.ConnectionString);
        var criteria = new StudySearchCriteria
        {
            Statuses = new[] { "RECRUITING" },
            EnrollmentMin = 100
        };

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var total = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.IsTrue(total >= results.Count, "Total count should be >= page results");
    }
}
