using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

/// <summary>
/// Integration test for advanced search with complex multi-filter combinations.
/// Tests the full search pipeline with multiple filters applied simultaneously.
/// </summary>
[TestClass]
public sealed class AdvancedSearchApiIntegrationTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchStudies_ComplexMultiFilter_ReturnsCorrectResults()
    {
        // Arrange: Test combining RECRUITING status with enrollment and location filters
        // This tests the core filtering pipeline works with multiple dimensions
        var criteria = new StudySearchCriteria
        {
            Statuses = new[] { "RECRUITING" },
            EnrollmentMin = 50,
            EnrollmentMax = 5000,
            Countries = new[] { "United States" },
            Page = 1,
            PageSize = 50
        };
        var repo = new StudyRepository(ConnectionString);

        // Act: Execute complex search
        var results = await repo.SearchStudiesAsync(criteria);
        var totalCount = await repo.CountStudiesFilteredAsync(criteria);

        // Assert: Verify results satisfy ALL filter criteria
        Assert.AreEqual(results.Count, totalCount, "Returned count should match total count");
        Assert.IsTrue(results.Count <= 50, "Results should respect pageSize");

        // Verify each result satisfies all applied filters
        foreach (var study in results)
        {
            // Status filter
            Assert.AreEqual("RECRUITING", study.OverallStatus, 
                $"Study {study.NctId} should have RECRUITING status");

            // Enrollment filter
            Assert.IsTrue(study.EnrollmentCount >= 50 && study.EnrollmentCount <= 5000,
                $"Study {study.NctId} enrollment ({study.EnrollmentCount}) should be 50-5000");

            // Location filter
            Assert.IsNotNull(study.Locations, $"Study {study.NctId} should have locations");
            Assert.IsTrue(study.Locations.Any(l => l.Country == "United States"),
                $"Study {study.NctId} should be in United States");
        }

        // Verify pagination works with combined filters
        if (totalCount > 5)
        {
            var page1Results = await repo.SearchStudiesAsync(new StudySearchCriteria
            {
                Statuses = criteria.Statuses,
                EnrollmentMin = criteria.EnrollmentMin,
                EnrollmentMax = criteria.EnrollmentMax,
                Countries = criteria.Countries,
                Page = 1,
                PageSize = 5
            });

            var page2Results = await repo.SearchStudiesAsync(new StudySearchCriteria
            {
                Statuses = criteria.Statuses,
                EnrollmentMin = criteria.EnrollmentMin,
                EnrollmentMax = criteria.EnrollmentMax,
                Countries = criteria.Countries,
                Page = 2,
                PageSize = 5
            });

            Assert.AreEqual(5, page1Results.Count, "Page 1 should have exactly 5 results");
            Assert.IsTrue(page2Results.Count > 0, "Page 2 should have results");
            
            // Verify no overlap between pages
            var page1Ids = page1Results.Select(s => s.NctId).ToHashSet();
            var page2Ids = page2Results.Select(s => s.NctId).ToHashSet();
            Assert.AreEqual(0, page1Ids.Intersect(page2Ids).Count(), 
                "Pages should not have overlapping results");
        }
    }
}
