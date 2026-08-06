using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class StudySearchFilterTests
{
    private static StudyEntity Study(
        string nctId,
        string? title = null,
        string? status = null,
        string? summary = null,
        int? enrollment = null,
        DateOnly? startDate = null,
        DateTime? removedAt = null,
        string[]? phases = null,
        string[]? conditionNames = null,
        string[]? conditionTreePaths = null,
        string[]? locationTrees = null,
        string? country = null,
        string? state = null,
        string? city = null,
        string? facility = null,
        string[]? keywords = null)
    {
        var conditions = conditionNames is null && conditionTreePaths is null
            ? null
            : conditionNames?.Select(n => new StudyConditionEntity { MeshDescriptor = new MeshDescriptorEntity { Name = n, TreeNumbers = locationTrees ?? [] } })
                .ToList();
        if (conditions is null && locationTrees is not null)
        {
            conditions = [new StudyConditionEntity { MeshDescriptor = new MeshDescriptorEntity { Name = "Placeholder", TreeNumbers = locationTrees } }];
        }

        return new StudyEntity
        {
            NctId = nctId,
            BriefTitle = title,
            BriefSummary = summary,
            OverallStatus = status,
            EnrollmentCount = enrollment,
            StartDate = startDate,
            RemovedFromSourceAt = removedAt,
            Phases = phases?.Select(p => new StudyPhaseEntity { Phase = p }).ToList(),
            Conditions = conditions,
            Locations = country is null && state is null && city is null && facility is null && locationTrees is null
                ? null
                : [new StudyLocationEntity
                {
                    Country = country,
                    State = state,
                    City = city,
                    Facility = facility,
                    MeshDescriptor = locationTrees is null ? null : new MeshDescriptorEntity { Name = "United States", TreeNumbers = locationTrees }
                }],
            Keywords = keywords?.Select(k => new StudyKeywordEntity { Keyword = k }).ToList() ?? []
        };
    }

    private static List<StudyEntity> Filter(StudySearchCriteria criteria, params StudyEntity[] studies)
    {
        return StudySearchFilter.ApplyFilters(studies.AsQueryable(), criteria).ToList();
    }

    [TestMethod]
    public void Keyword_MatchesBriefTitleCaseInsensitively()
    {
        var studies = new[]
        {
            Study("NCT1", title: "Pregabalin for neuropathy"),
            Study("NCT2", title: "Aspirin trial")
        };

        var results = Filter(new StudySearchCriteria { Keyword = "PREGABALIN" }, studies);

        CollectionAssert.AreEqual(new[] { "NCT1" }, results.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void Keyword_MatchesOfficialTitleAndSummary()
    {
        var studies = new[]
        {
            Study("NCT1", title: "Study 1", summary: "evaluates metformin safety"),
            Study("NCT2", title: "Metformin extended release", summary: "nothing here"),
            Study("NCT3", title: "Study 3", summary: "unrelated")
        };

        var results = Filter(new StudySearchCriteria { Keyword = "metformin" }, studies);

        CollectionAssert.AreEqual(new[] { "NCT1", "NCT2" }, results.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void Keyword_MatchesNctId()
    {
        var studies = new[]
        {
            Study("NCT01234567"),
            Study("NCT99999999")
        };

        var results = Filter(new StudySearchCriteria { Keyword = "nct01234567" }, studies);

        CollectionAssert.AreEqual(new[] { "NCT01234567" }, results.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void Keyword_MatchesStudyKeywordsCaseInsensitively()
    {
        var studies = new[]
        {
            Study("NCT1", title: "Study of an intervention", keywords: ["Diabetic Ketoacidosis"]),
            Study("NCT2", title: "Unrelated trial", keywords: ["Aspirin"])
        };

        var results = Filter(new StudySearchCriteria { Keyword = "ketoacidosis" }, studies);

        CollectionAssert.AreEqual(new[] { "NCT1" }, results.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void Keyword_MatchesTitleOrKeywords_WithinDimension()
    {
        var studies = new[]
        {
            Study("NCT1", title: "Pregabalin for neuropathy", keywords: ["Pain"]),
            Study("NCT2", title: "Aspirin trial", keywords: ["Pregabalin dosing"]),
            Study("NCT3", title: "Placebo study", keywords: ["Safety"])
        };

        var results = Filter(new StudySearchCriteria { Keyword = "pregabalin" }, studies);

        CollectionAssert.AreEqual(new[] { "NCT1", "NCT2" }, results.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void Keyword_NoMatch_ReturnsEmpty()
    {
        var studies = new[] { Study("NCT1", title: "Aspirin trial") };

        var results = Filter(new StudySearchCriteria { Keyword = "cancer" }, studies);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void Status_FiltersSingleAndMultiple()
    {
        var studies = new[]
        {
            Study("NCT1", status: "RECRUITING"),
            Study("NCT2", status: "COMPLETED"),
            Study("NCT3", status: "TERMINATED")
        };

        var single = Filter(new StudySearchCriteria { Statuses = ["RECRUITING"] }, studies);
        var multi = Filter(new StudySearchCriteria { Statuses = ["RECRUITING", "COMPLETED"] }, studies);

        CollectionAssert.AreEqual(new[] { "NCT1" }, single.Select(s => s.NctId).ToList());
        CollectionAssert.AreEqual(new[] { "NCT1", "NCT2" }, multi.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void Phase_FiltersMultiple()
    {
        var studies = new[]
        {
            Study("NCT1", phases: ["PHASE1"]),
            Study("NCT2", phases: ["PHASE2"]),
            Study("NCT3", phases: ["PHASE3"]),
            Study("NCT4")
        };

        var results = Filter(new StudySearchCriteria { Phases = ["PHASE1", "PHASE3"] }, studies);

        CollectionAssert.AreEqual(new[] { "NCT1", "NCT3" }, results.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void Condition_MatchesExactDescriptorNames()
    {
        var studies = new[]
        {
            Study("NCT1", conditionNames: ["Hypertension"]),
            Study("NCT2", conditionNames: ["Diabetes Mellitus"]),
            Study("NCT3", conditionNames: ["Hypertension", "Obesity"]),
            Study("NCT4")
        };

        var results = Filter(new StudySearchCriteria { Conditions = ["Hypertension"] }, studies);

        CollectionAssert.AreEqual(new[] { "NCT1", "NCT3" }, results.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void MeshTreePrefix_MatchesHierarchicalDescendants()
    {
        var studies = new[]
        {
            Study("NCT1", conditionNames: ["Diabetes Mellitus, Type 2"]),
            Study("NCT2", conditionNames: ["Obesity"])
        };
        studies[0].Conditions!.First().MeshDescriptor!.TreeNumberPaths = [new MeshTreePathEntity { TreeNumber = "C19.246.300" }];
        studies[1].Conditions!.First().MeshDescriptor!.TreeNumberPaths = [new MeshTreePathEntity { TreeNumber = "C23.888" }];

        var results = Filter(new StudySearchCriteria { MeshTreePrefixes = ["C19.246"] }, studies);

        CollectionAssert.AreEqual(new[] { "NCT1" }, results.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void LocationRawFilters_MatchCountryStateCityFacility()
    {
        var studies = new[]
        {
            Study("NCT1", country: "United States", state: "CA", city: "San Francisco", facility: "Stanford"),
            Study("NCT2", country: "Canada", state: "ON", city: "Toronto", facility: "SickKids"),
            Study("NCT3", country: "United States", state: "NY", city: "New York", facility: "Mount Sinai")
        };

        var byCountry = Filter(new StudySearchCriteria { Countries = ["United States"] }, studies);
        var byState = Filter(new StudySearchCriteria { States = ["CA"] }, studies);
        var byCity = Filter(new StudySearchCriteria { Cities = ["Toronto"] }, studies);
        var byFacility = Filter(new StudySearchCriteria { Facilities = ["Mount Sinai"] }, studies);

        CollectionAssert.AreEqual(new[] { "NCT1", "NCT3" }, byCountry.Select(s => s.NctId).ToList());
        CollectionAssert.AreEqual(new[] { "NCT1" }, byState.Select(s => s.NctId).ToList());
        CollectionAssert.AreEqual(new[] { "NCT2" }, byCity.Select(s => s.NctId).ToList());
        CollectionAssert.AreEqual(new[] { "NCT3" }, byFacility.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void LocationMeshTreePrefix_FiltersByZGeographicalPrefix()
    {
        var studies = new[]
        {
            Study("NCT1", locationTrees: ["Z01.107.567.875"], country: "United States"),
            Study("NCT2", locationTrees: ["Z01.107.567.875.510.550"], country: "United States"),
            Study("NCT3", locationTrees: ["Z01.107.084"], country: "Canada")
        };

        var results = Filter(new StudySearchCriteria { LocationMeshTreePrefixes = ["Z01.107.567.875"] }, studies);

        CollectionAssert.AreEqual(new[] { "NCT1", "NCT2" }, results.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void EnrollmentRange_FiltersMinMax()
    {
        var studies = new[]
        {
            Study("NCT1", enrollment: 50),
            Study("NCT2", enrollment: 200),
            Study("NCT3", enrollment: 1000),
            Study("NCT4")
        };

        var min = Filter(new StudySearchCriteria { EnrollmentMin = 200 }, studies);
        var max = Filter(new StudySearchCriteria { EnrollmentMax = 200 }, studies);
        var range = Filter(new StudySearchCriteria { EnrollmentMin = 100, EnrollmentMax = 500 }, studies);

        CollectionAssert.AreEqual(new[] { "NCT2", "NCT3" }, min.Select(s => s.NctId).ToList());
        CollectionAssert.AreEqual(new[] { "NCT1", "NCT2" }, max.Select(s => s.NctId).ToList());
        CollectionAssert.AreEqual(new[] { "NCT2" }, range.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void DateRange_FiltersFromTo()
    {
        var studies = new[]
        {
            Study("NCT1", startDate: new DateOnly(2020, 1, 1)),
            Study("NCT2", startDate: new DateOnly(2023, 6, 15)),
            Study("NCT3", startDate: new DateOnly(2025, 12, 31)),
            Study("NCT4")
        };

        var from = Filter(new StudySearchCriteria { StartDateFrom = new DateTime(2023, 1, 1) }, studies);
        var to = Filter(new StudySearchCriteria { StartDateTo = new DateTime(2023, 12, 31) }, studies);

        CollectionAssert.AreEqual(new[] { "NCT2", "NCT3" }, from.Select(s => s.NctId).ToList());
        CollectionAssert.AreEqual(new[] { "NCT1", "NCT2" }, to.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void RemovedStudies_ExcludedByDefaultIncludedWhenRequested()
    {
        var studies = new[]
        {
            Study("NCT1", removedAt: DateTime.UtcNow),
            Study("NCT2")
        };

        var defaultResults = Filter(new StudySearchCriteria(), studies);
        var includeRemoved = Filter(new StudySearchCriteria { IncludeRemoved = true }, studies);

        CollectionAssert.AreEqual(new[] { "NCT2" }, defaultResults.Select(s => s.NctId).ToList());
        CollectionAssert.AreEqual(new[] { "NCT1", "NCT2" }, includeRemoved.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void MultipleDimensions_CombineWithAnd()
    {
        var studies = new[]
        {
            Study("NCT1", status: "RECRUITING", conditionNames: ["Hypertension"], country: "United States", phases: ["PHASE2"]),
            Study("NCT2", status: "RECRUITING", conditionNames: ["Hypertension"], country: "Canada", phases: ["PHASE2"]),
            Study("NCT3", status: "COMPLETED", conditionNames: ["Hypertension"], country: "United States", phases: ["PHASE2"]),
            Study("NCT4", status: "RECRUITING", conditionNames: ["Obesity"], country: "United States", phases: ["PHASE2"]),
            Study("NCT5", status: "RECRUITING", conditionNames: ["Hypertension"], country: "United States", phases: ["PHASE1"])
        };

        var results = Filter(new StudySearchCriteria
        {
            Statuses = ["RECRUITING"],
            Conditions = ["Hypertension"],
            Countries = ["United States"],
            Phases = ["PHASE2"]
        }, studies);

        CollectionAssert.AreEqual(new[] { "NCT1" }, results.Select(s => s.NctId).ToList());
    }

    [TestMethod]
    public void EmptyCriteria_ReturnsAllStudies()
    {
        var studies = new[]
        {
            Study("NCT1", title: "A"),
            Study("NCT2", title: "B"),
            Study("NCT3", title: "C")
        };

        var results = Filter(new StudySearchCriteria(), studies);

        Assert.AreEqual(3, results.Count);
    }
}
