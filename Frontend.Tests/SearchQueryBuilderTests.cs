using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class SearchQueryBuilderTests
{
    private static SearchCriteria NewCriteria() => new();

    [TestMethod]
    public void BuildStudiesQueryNoFiltersOnlyPageAndPageSize()
    {
        var query = SearchQueryBuilder.BuildStudiesQuery(2, 10, NewCriteria());

        Assert.AreEqual("page=2&pageSize=10", query);
    }

    [TestMethod]
    public void BuildStudiesQueryKeywordUriEscaped()
    {
        var criteria = NewCriteria();
        criteria.Keyword = "lung & cancer";

        var query = SearchQueryBuilder.BuildStudiesQuery(1, 10, criteria);

        StringAssert.Contains(query, "keyword=lung%20%26%20cancer", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildStudiesQueryStatusesCommaJoinedAndEscaped()
    {
        var criteria = NewCriteria();
        criteria.Statuses = ["RECRUITING", "ACTIVE"];

        var query = SearchQueryBuilder.BuildStudiesQuery(1, 10, criteria);

        StringAssert.Contains(query, "status=RECRUITING%2CACTIVE", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildStudiesQueryPhasesCommaJoined()
    {
        var criteria = NewCriteria();
        criteria.Phases = ["PHASE3", "PHASE4"];

        var query = SearchQueryBuilder.BuildStudiesQuery(1, 10, criteria);

        StringAssert.Contains(query, "phase=PHASE3%2CPHASE4", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildStudiesQueryMeshTreePrefixesCommaJoined()
    {
        var criteria = NewCriteria();
        criteria.MeshTreePrefixes = ["C19", "C19.246"];

        var query = SearchQueryBuilder.BuildStudiesQuery(1, 10, criteria);

        StringAssert.Contains(query, "meshTree=C19%2CC19.246", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildStudiesQueryConditionsCommaJoinedAndEscaped()
    {
        var criteria = NewCriteria();
        criteria.Conditions = ["Diabetes Mellitus", "Hypertension"];

        var query = SearchQueryBuilder.BuildStudiesQuery(1, 10, criteria);

        StringAssert.Contains(query, "condition=Diabetes%20Mellitus%2CHypertension", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildStudiesQueryEnrollmentBoundsRawValues()
    {
        var criteria = NewCriteria();
        criteria.EnrollmentMin = 100;
        criteria.EnrollmentMax = 5000;

        var query = SearchQueryBuilder.BuildStudiesQuery(1, 10, criteria);

        StringAssert.Contains(query, "enrollmentMin=100", StringComparison.Ordinal);
        StringAssert.Contains(query, "enrollmentMax=5000", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildStudiesQueryStartDatesFormattedAsYyyyMmDd()
    {
        var criteria = NewCriteria();
        criteria.StartDateFrom = new DateTime(2020, 1, 5);
        criteria.StartDateTo = new DateTime(2024, 12, 31);

        var query = SearchQueryBuilder.BuildStudiesQuery(1, 10, criteria);

        StringAssert.Contains(query, "startDateFrom=2020-01-05", StringComparison.Ordinal);
        StringAssert.Contains(query, "startDateTo=2024-12-31", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildStudiesQueryAllFiltersOnlyIncludedWhenSet()
    {
        var criteria = NewCriteria();
        criteria.Keyword = "cancer";
        criteria.Statuses = ["RECRUITING"];
        criteria.Phases = ["PHASE2"];
        criteria.MeshTreePrefixes = ["C04"];
        criteria.Conditions = ["Melanoma"];
        criteria.EnrollmentMin = 10;
        criteria.EnrollmentMax = 20;
        criteria.StartDateFrom = new DateTime(2021, 3, 1);
        criteria.StartDateTo = new DateTime(2022, 4, 2);

        var query = SearchQueryBuilder.BuildStudiesQuery(3, 25, criteria);

        Assert.AreEqual(
            "page=3&pageSize=25&keyword=cancer&status=RECRUITING&phase=PHASE2" +
            "&meshTree=C04&condition=Melanoma&enrollmentMin=10&enrollmentMax=20" +
            "&startDateFrom=2021-03-01&startDateTo=2022-04-02",
            query);
    }
}
