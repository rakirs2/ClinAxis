using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class SearchUrlParserTests
{
    private static readonly string[] TwoStatuses = ["RECRUITING", "COMPLETED"];
    private static readonly string[] OnePhase = ["PHASE2"];
    private static readonly string[] TwoMeshPrefixes = ["C04", "C04.588"];
    private static readonly string[] TwoConditions = ["Diabetes Mellitus", "Hypertension"];
    private static readonly string[] EmptyCriteriaList = [];

    [TestMethod]
    public void ParseNoQueryReturnsEmptyCriteriaAndPageOne()
    {
        var (criteria, page) = SearchUrlParser.Parse("http://localhost:5001/");

        Assert.AreEqual(1, page);
        Assert.IsNull(criteria.Keyword);
        Assert.AreEqual(0, criteria.Statuses.Count);
        Assert.AreEqual(0, criteria.Phases.Count);
        Assert.AreEqual(0, criteria.Conditions.Count);
        Assert.AreEqual(0, criteria.MeshTreePrefixes.Count);
        Assert.IsNull(criteria.EnrollmentMin);
        Assert.IsNull(criteria.EnrollmentMax);
        Assert.IsNull(criteria.StartDateFrom);
        Assert.IsNull(criteria.StartDateTo);
    }

    [TestMethod]
    public void ParseFullQueryPopulatesAllFilters()
    {
        var url = "http://localhost:5001/?page=3&pageSize=25&keyword=cancer&status=RECRUITING%2CCOMPLETED" +
                  "&phase=PHASE2&meshTree=C04%2CC04.588&condition=Diabetes%20Mellitus%2CHypertension" +
                  "&enrollmentMin=10&enrollmentMax=500" +
                  "&startDateFrom=2021-03-01&startDateTo=2022-04-02";

        var (criteria, page) = SearchUrlParser.Parse(url);

        Assert.AreEqual(3, page);
        Assert.AreEqual("cancer", criteria.Keyword);
        CollectionAssert.AreEqual(TwoStatuses, criteria.Statuses.ToArray());
        CollectionAssert.AreEqual(OnePhase, criteria.Phases.ToArray());
        CollectionAssert.AreEqual(TwoMeshPrefixes, criteria.MeshTreePrefixes.ToArray());
        CollectionAssert.AreEqual(TwoConditions, criteria.Conditions.ToArray());
        Assert.AreEqual(10, criteria.EnrollmentMin);
        Assert.AreEqual(500, criteria.EnrollmentMax);
        Assert.AreEqual(new DateTime(2021, 3, 1), criteria.StartDateFrom);
        Assert.AreEqual(new DateTime(2022, 4, 2), criteria.StartDateTo);
    }

    [TestMethod]
    public void ParseInvalidNumbersAndDatesAreIgnored()
    {
        var url = "http://localhost:5001/?page=abc&enrollmentMin=xyz&enrollmentMax=0&startDateFrom=not-a-date&startDateTo=2024-13-99";

        var (criteria, page) = SearchUrlParser.Parse(url);

        Assert.AreEqual(1, page);
        Assert.IsNull(criteria.EnrollmentMin);
        Assert.AreEqual(0, criteria.EnrollmentMax);
        Assert.IsNull(criteria.StartDateFrom);
        Assert.IsNull(criteria.StartDateTo);
    }

    [TestMethod]
    public void ParseEmptyValuesProduceNoFilters()
    {
        var url = "http://localhost:5001/?status=&condition=&keyword=";

        var (criteria, page) = SearchUrlParser.Parse(url);

        Assert.AreEqual(1, page);
        Assert.IsNull(criteria.Keyword);
        Assert.AreEqual(0, criteria.Statuses.Count);
        Assert.AreEqual(0, criteria.Conditions.Count);
    }

    [TestMethod]
    public void ParseUnknownKeysAreIgnored()
    {
        var url = "http://localhost:5001/?unknown=value&foo=bar";

        var (criteria, page) = SearchUrlParser.Parse(url);

        Assert.AreEqual(1, page);
        Assert.IsNull(criteria.Keyword);
    }

    [TestMethod]
    public void ParseIsRoundTripWithSearchQueryBuilder()
    {
        var criteria = new SearchCriteria
        {
            Keyword = "lung & cancer",
            Statuses = ["RECRUITING", "ACTIVE"],
            Phases = ["PHASE2", "PHASE3"],
            Conditions = ["Diabetes Mellitus", "Hypertension"],
            MeshTreePrefixes = ["C19", "C19.246"],
            EnrollmentMin = 100,
            EnrollmentMax = 5000,
            StartDateFrom = new DateTime(2020, 1, 5),
            StartDateTo = new DateTime(2024, 12, 31)
        };

        var query = SearchQueryBuilder.BuildStudiesQuery(2, 10, criteria);
        var (parsed, page) = SearchUrlParser.Parse($"/?{query}");

        Assert.AreEqual(2, page);
        Assert.AreEqual(criteria.Keyword, parsed.Keyword);
        CollectionAssert.AreEqual(criteria.Statuses.ToArray(), parsed.Statuses.ToArray());
        CollectionAssert.AreEqual(criteria.Phases.ToArray(), parsed.Phases.ToArray());
        CollectionAssert.AreEqual(criteria.Conditions.ToArray(), parsed.Conditions.ToArray());
        CollectionAssert.AreEqual(criteria.MeshTreePrefixes.ToArray(), parsed.MeshTreePrefixes.ToArray());
        Assert.AreEqual(criteria.EnrollmentMin, parsed.EnrollmentMin);
        Assert.AreEqual(criteria.EnrollmentMax, parsed.EnrollmentMax);
        Assert.AreEqual(criteria.StartDateFrom, parsed.StartDateFrom);
        Assert.AreEqual(criteria.StartDateTo, parsed.StartDateTo);
    }

    [TestMethod]
    public void ParseEmptyCriteriaRoundTripsToEmptyQuery()
    {
        var query = SearchQueryBuilder.BuildStudiesQuery(1, 10, new SearchCriteria());
        var (criteria, page) = SearchUrlParser.Parse($"/?{query}");

        Assert.AreEqual(1, page);
        Assert.IsNull(criteria.Keyword);
        CollectionAssert.AreEqual(EmptyCriteriaList, criteria.Statuses.ToArray());
    }
}
