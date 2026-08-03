using DataApi.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataApi.Tests;

[TestClass]
public sealed class PiFeatureRowBuilderTests
{
    private static readonly Guid PersonId = Guid.NewGuid();
    private static readonly DateOnly StartDate = new(2026, 1, 1);

    [TestMethod]
    public void BuildRowWithNoDataEmitsZeroFlags()
    {
        var target = Target("COMPLETED");

        var row = PiFeatureRowBuilder.BuildRow(target, HistoryFeatures.Empty, [], [], [], null);

        Assert.AreEqual("1", Col(row, "label"));
        Assert.AreEqual("0", Col(row, "has_medicare"));
        Assert.AreEqual("0", Col(row, "has_payments"));
        Assert.AreEqual("0", Col(row, "has_metrics"));
        Assert.AreEqual("0", Col(row, "prior_study_count"));
        Assert.AreEqual("0", Col(row, "papers_before_start"));
        Assert.AreEqual("0", Col(row, "payor_count"));
    }

    [TestMethod]
    public void BuildRowTerminatedStudyGetsZeroLabel()
    {
        var row = PiFeatureRowBuilder.BuildRow(Target("TERMINATED"), HistoryFeatures.Empty, [], [], [], null);

        Assert.AreEqual("0", Col(row, "label"));
        Assert.AreEqual("TERMINATED", Col(row, "overall_status"));
    }

    [TestMethod]
    public void BuildRowCountsOnlyPapersStrictlyBeforeStartDate()
    {
        var papers = new List<DateOnly>
        {
            new(2020, 6, 1),
            new(2025, 12, 31),
            StartDate,
            new(2026, 6, 1)
        };

        var row = PiFeatureRowBuilder.BuildRow(Target("COMPLETED"), HistoryFeatures.Empty, papers, [], [], null);

        Assert.AreEqual("2", Col(row, "papers_before_start"));
    }

    [TestMethod]
    public void BuildRowPapersPerYearSpansFromFirstPaperToStartYear()
    {
        var papers = new List<DateOnly>
        {
            new(2024, 3, 1),
            new(2025, 3, 1),
            new(2025, 9, 1)
        };

        var row = PiFeatureRowBuilder.BuildRow(Target("COMPLETED"), HistoryFeatures.Empty, papers, [], [], null);

        Assert.AreEqual("3", Col(row, "papers_before_start"));
        Assert.AreEqual("1", Col(row, "papers_per_year_before_start"));
    }

    [TestMethod]
    public void BuildRowMedicareFiltersDataYearAndAveragesRisk()
    {
        var medicare = new List<MedicareRow>
        {
            new(2024, 100, 250, 5000m, 2.0m),
            new(2023, 50, 120, 1000m, 1.0m),
            new(2027, 999, 999, 99999m, 9.0m)
        };

        var row = PiFeatureRowBuilder.BuildRow(Target("COMPLETED"), HistoryFeatures.Empty, [], medicare, [], null);

        Assert.AreEqual("1", Col(row, "has_medicare"));
        Assert.AreEqual("150", Col(row, "medicare_beneficiaries"));
        Assert.AreEqual("370", Col(row, "medicare_services"));
        Assert.AreEqual("6000", Col(row, "medicare_payments"));
        Assert.AreEqual("1.5", Col(row, "medicare_risk_score"));
    }

    [TestMethod]
    public void BuildRowMedicareNullRiskScoresExcludedFromAverage()
    {
        var medicare = new List<MedicareRow>
        {
            new(2024, 10, 10, 100m, null),
            new(2024, 10, 10, 100m, 1.0m)
        };

        var row = PiFeatureRowBuilder.BuildRow(Target("COMPLETED"), HistoryFeatures.Empty, [], medicare, [], null);

        Assert.AreEqual("1", Col(row, "medicare_risk_score"));
    }

    [TestMethod]
    public void BuildRowOpenPaymentsSplitsResearchAndGeneral()
    {
        var payments = new List<OpenPaymentRow>
        {
            new(2024, "research", 500m, "NIH"),
            new(2024, "research", 100m, "NIH"),
            new(2024, "general", 250m, "Medtronic"),
            new(2027, "general", 900m, "FutureCo")
        };

        var row = PiFeatureRowBuilder.BuildRow(Target("COMPLETED"), HistoryFeatures.Empty, [], [], payments, null);

        Assert.AreEqual("1", Col(row, "has_payments"));
        Assert.AreEqual("600", Col(row, "research_payments"));
        Assert.AreEqual("250", Col(row, "general_payments"));
    }

    [TestMethod]
    public void BuildRowPayorCountIsDistinctAndSkipsBlank()
    {
        var payments = new List<OpenPaymentRow>
        {
            new(2024, "research", 100m, "NIH"),
            new(2024, "general", 200m, "NIH"),
            new(2024, "general", 300m, "Medtronic"),
            new(2024, "general", 400m, ""),
            new(2024, "general", 500m, "   ")
        };

        var row = PiFeatureRowBuilder.BuildRow(Target("COMPLETED"), HistoryFeatures.Empty, [], [], payments, null);

        Assert.AreEqual("2", Col(row, "payor_count"));
    }

    [TestMethod]
    public void BuildRowMetricsFoundEmitsValuesAndFlag()
    {
        var metrics = new MetricsRow(12, 345, 6, 89, PiFeatureRowBuilder.Found);

        var row = PiFeatureRowBuilder.BuildRow(Target("COMPLETED"), HistoryFeatures.Empty, [], [], [], metrics);

        Assert.AreEqual("1", Col(row, "has_metrics"));
        Assert.AreEqual("12", Col(row, "current_h_index"));
        Assert.AreEqual("345", Col(row, "citation_count"));
        Assert.AreEqual("6", Col(row, "i10_index"));
        Assert.AreEqual("89", Col(row, "total_papers"));
    }

    [TestMethod]
    public void BuildRowMetricsLookupNotFoundEmitsZeroFlagButKeepsValues()
    {
        var metrics = new MetricsRow(12, 345, 6, 89, "not_found");

        var row = PiFeatureRowBuilder.BuildRow(Target("COMPLETED"), HistoryFeatures.Empty, [], [], [], metrics);

        Assert.AreEqual("0", Col(row, "has_metrics"));
        Assert.AreEqual("12", Col(row, "current_h_index"));
        Assert.AreEqual("89", Col(row, "total_papers"));
    }

    [TestMethod]
    public void BuildRowFullNameIsQuotedAndEscaped()
    {
        var target = Target("COMPLETED") with { FullName = "Jane \"Doc\" Smith" };

        var row = PiFeatureRowBuilder.BuildRow(target, HistoryFeatures.Empty, [], [], [], null);

        Assert.AreEqual("\"Jane \"\"Doc\"\" Smith\"", Col(row, "full_name"));
    }

    [TestMethod]
    public void ComputeHistoryFeaturesAggregatesDistinctStudies()
    {
        var studies = new List<HistoryStudyRow>
        {
            new("NCT1", "COMPLETED", 100),
            new("NCT1", "COMPLETED", 100),
            new("NCT2", "TERMINATED", 50),
            new("NCT3", "COMPLETED", null)
        };

        var features = PiFeatureRowBuilder.ComputeHistoryFeatures(studies);

        Assert.AreEqual(3, features.StudyCount);
        Assert.AreEqual(2, features.CompletedCount);
        Assert.AreEqual(150L, features.EnrollmentTotal);
        Assert.AreEqual(2.0 / 3.0, features.CompletionRate, 0.0001);
    }

    [TestMethod]
    public void ComputeHistoryFeaturesEmptyReturnsZeroRate()
    {
        var features = PiFeatureRowBuilder.ComputeHistoryFeatures([]);

        Assert.AreEqual(0, features.StudyCount);
        Assert.AreEqual(0, features.CompletedCount);
        Assert.AreEqual(0L, features.EnrollmentTotal);
        Assert.AreEqual(0.0, features.CompletionRate);
    }

    private static TargetRow Target(string overallStatus) =>
        new("NCT00000001", PersonId, "Jane Smith", StartDate, overallStatus, 100);

    private static string Col(string[] row, string header)
    {
        var index = Array.IndexOf(PiFeatureRowBuilder.Header, header);
        Assert.IsTrue(index >= 0, $"Header '{header}' not found");
        return row[index];
    }
}
