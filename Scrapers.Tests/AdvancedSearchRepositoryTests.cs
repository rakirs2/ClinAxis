using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Testing;

namespace Scrapers.Tests;

/// <summary>
/// Unit tests for advanced search filtering on individual filter dimensions.
/// Tests verify the search mechanism works correctly with various filter combinations.
/// </summary>
[TestClass]
public sealed class AdvancedSearchRepositoryTests : DbTestBase
{
    [TestMethod]
    public async Task SearchStudies_WithEmptyFilters_ReturnsAll()
    {
        // Arrange
        var criteria = new StudySearchCriteria
        {
            Page = 1,
            PageSize = 100
        };
        var repo = new StudyRepository(ConnectionString);

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var count = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.IsTrue(count >= 0, "Count should return valid number");
        Assert.AreEqual(count, results.Count, "Results count should match total count for page 1");
    }

    [TestMethod]
    public async Task SearchStudies_ByKeyword_ReturnsCorrectResults()
    {
        // Arrange
        var criteria = new StudySearchCriteria
        {
            Keyword = "Study",
            Page = 1,
            PageSize = 100
        };
        var repo = new StudyRepository(ConnectionString);

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var count = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.AreEqual(count, results.Count, "Count should match results");
        // Verify all results match keyword if any exist
        if (results.Count > 0)
        {
            foreach (var study in results)
            {
                bool hasKeyword = 
                    (study.BriefTitle != null && study.BriefTitle.Contains("Study", StringComparison.OrdinalIgnoreCase)) ||
                    (study.OfficialTitle != null && study.OfficialTitle.Contains("Study", StringComparison.OrdinalIgnoreCase)) ||
                    (study.BriefSummary != null && study.BriefSummary.Contains("Study", StringComparison.OrdinalIgnoreCase)) ||
                    study.NctId.Contains("Study", StringComparison.OrdinalIgnoreCase);
                Assert.IsTrue(hasKeyword, $"Study {study.NctId} should contain keyword");
            }
        }
    }

    [TestMethod]
    public async Task SearchStudies_ByStatus_FiltersCorrectly()
    {
        // Arrange
        var criteria = new StudySearchCriteria
        {
            Statuses = new[] { "RECRUITING" },
            Page = 1,
            PageSize = 100
        };
        var repo = new StudyRepository(ConnectionString);

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var count = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.AreEqual(count, results.Count, "Count should match results");
        Assert.IsTrue(results.All(s => s.OverallStatus == "RECRUITING"), 
            "All results should have RECRUITING status");
    }

    [TestMethod]
    public async Task SearchStudies_ByStatus_MultipleValues_FiltersCorrectly()
    {
        // Arrange
        var criteria = new StudySearchCriteria
        {
            Statuses = new[] { "RECRUITING", "ACTIVE" },
            Page = 1,
            PageSize = 100
        };
        var repo = new StudyRepository(ConnectionString);

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var count = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.AreEqual(count, results.Count, "Count should match results");
        Assert.IsTrue(results.All(s => s.OverallStatus == "RECRUITING" || s.OverallStatus == "ACTIVE"),
            "All results should have RECRUITING or ACTIVE status");
    }

    [TestMethod]
    public async Task SearchStudies_ByPhase_FiltersCorrectly()
    {
        // Arrange
        var criteria = new StudySearchCriteria
        {
            Phases = new[] { "PHASE3" },
            Page = 1,
            PageSize = 100
        };
        var repo = new StudyRepository(ConnectionString);

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var count = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.AreEqual(count, results.Count, "Count should match results");
        Assert.IsTrue(results.All(s => s.Phases != null && s.Phases.Any(p => p.Phase == "PHASE3")),
            "All results should have PHASE3");
    }

    [TestMethod]
    public async Task SearchStudies_ByPhase_MultipleValues_FiltersCorrectly()
    {
        // Arrange
        var criteria = new StudySearchCriteria
        {
            Phases = new[] { "PHASE2", "PHASE4" },
            Page = 1,
            PageSize = 100
        };
        var repo = new StudyRepository(ConnectionString);

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var count = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.AreEqual(count, results.Count, "Count should match results");
        Assert.IsTrue(results.All(s => s.Phases != null && s.Phases.Any(p => p.Phase == "PHASE2" || p.Phase == "PHASE4")),
            "All results should have PHASE2 or PHASE4");
    }

    [TestMethod]
    public async Task SearchStudies_ByPhase_NA_FiltersCorrectly()
    {
        // Arrange
        var criteria = new StudySearchCriteria
        {
            Phases = new[] { "NA" },
            Page = 1,
            PageSize = 100
        };
        var repo = new StudyRepository(ConnectionString);

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var count = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.AreEqual(count, results.Count, "Count should match results");
        Assert.IsTrue(results.All(s => s.Phases != null && s.Phases.Any(p => p.Phase == "NA")),
            "All results should have NA phase");
    }

    [TestMethod]
    public async Task SearchStudies_ByCondition_FiltersCorrectly()
    {
        // Arrange - Using actual seed data condition
        var criteria = new StudySearchCriteria
        {
            Conditions = new[] { "Hypertension" },
            Page = 1,
            PageSize = 100
        };
        var repo = new StudyRepository(ConnectionString);

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var count = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.AreEqual(count, results.Count, "Count should match results");
        Assert.IsTrue(results.All(s => s.Conditions != null && s.Conditions.Any(c => c.Condition == "Hypertension")),
            "All results should have Hypertension condition");
    }

    [TestMethod]
    public async Task SearchStudies_ByEnrollmentMin_FiltersCorrectly()
    {
        // Arrange
        var criteria = new StudySearchCriteria
        {
            EnrollmentMin = 100,
            Page = 1,
            PageSize = 100
        };
        var repo = new StudyRepository(ConnectionString);

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var count = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.AreEqual(count, results.Count, "Count should match results");
        Assert.IsTrue(results.All(s => s.EnrollmentCount >= 100),
            "All results should have enrollment >= 100");
    }

    [TestMethod]
    public async Task SearchStudies_ByEnrollmentRange_FiltersCorrectly()
    {
        // Arrange
        var criteria = new StudySearchCriteria
        {
            EnrollmentMin = 100,
            EnrollmentMax = 300,
            Page = 1,
            PageSize = 100
        };
        var repo = new StudyRepository(ConnectionString);

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var count = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.AreEqual(count, results.Count, "Count should match results");
        Assert.IsTrue(results.All(s => s.EnrollmentCount >= 100 && s.EnrollmentCount <= 300),
            "All results should have enrollment between 100 and 300");
    }

    [TestMethod]
    public async Task SearchStudies_ByStartDateFrom_FiltersCorrectly()
    {
        // Arrange
        var criteria = new StudySearchCriteria
        {
            StartDateFrom = new DateTime(2024, 1, 1),
            Page = 1,
            PageSize = 100
        };
        var repo = new StudyRepository(ConnectionString);

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var count = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.AreEqual(count, results.Count, "Count should match results");
        Assert.IsTrue(results.All(s => s.StartDate >= new DateOnly(2024, 1, 1)),
            "All results should start from 2024-01-01 or later");
    }

    [TestMethod]
    public async Task SearchStudies_Pagination_Works()
    {
        // Arrange
        var criteriaPage1 = new StudySearchCriteria
        {
            Page = 1,
            PageSize = 5
        };
        var criteriaPage2 = new StudySearchCriteria
        {
            Page = 2,
            PageSize = 5
        };
        var repo = new StudyRepository(ConnectionString);

        // Act
        var page1 = await repo.SearchStudiesAsync(criteriaPage1);
        var page2 = await repo.SearchStudiesAsync(criteriaPage2);
        var totalCount = await repo.CountStudiesFilteredAsync(new StudySearchCriteria());

        // Assert
        if (totalCount <= 5)
        {
            Assert.AreEqual(0, page2.Count, "Page 2 should be empty if total <= 5");
        }
        else
        {
            Assert.AreEqual(5, page1.Count, "Page 1 should have 5 results");
            Assert.IsTrue(page2.Count > 0, "Page 2 should have results");
            
            // Verify no overlap
            var page1Ids = page1.Select(s => s.NctId).ToHashSet();
            var page2Ids = page2.Select(s => s.NctId).ToHashSet();
            Assert.AreEqual(0, page1Ids.Intersect(page2Ids).Count(), 
                "Pages should not overlap");
        }
    }

    [TestMethod]
    public async Task SearchStudies_CombinedFilters_FiltersCorrectly()
    {
        // Arrange: Test combining multiple filters
        var criteria = new StudySearchCriteria
        {
            Statuses = new[] { "RECRUITING" },
            EnrollmentMin = 100,
            Page = 1,
            PageSize = 100
        };
        var repo = new StudyRepository(ConnectionString);

        // Act
        var results = await repo.SearchStudiesAsync(criteria);
        var count = await repo.CountStudiesFilteredAsync(criteria);

        // Assert
        Assert.AreEqual(count, results.Count, "Count should match results");
        Assert.IsTrue(results.All(s => 
            s.OverallStatus == "RECRUITING" && s.EnrollmentCount >= 100),
            "All results should match both filters");
    }
}
