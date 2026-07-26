using DataApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;

namespace DataApi.Tests;

[TestClass]
public sealed class StudyMapperTests
{
    private static StudyEntity CreateFullStudy()
    {
        return new StudyEntity
        {
            NctId = "NCT001",
            BriefTitle = "Test Study",
            OfficialTitle = "A Test Study for Testing",
            OverallStatus = "RECRUITING",
            StudyType = "Interventional",
            BriefSummary = "This is a test summary",
            PrimaryPurpose = "Treatment",
            InterventionModel = "Parallel",
            Allocation = "Randomized",
            Masking = "Double",
            OrgStudyId = "ORG-001",
            LeadSponsorName = "Test Sponsor",
            CollaboratorNames = "Collab1, Collab2",
            EligibilityCriteria = "Inclusion: healthy",
            HealthyVolunteers = "Yes",
            EnrollmentCount = 100,
            Sex = "All",
            MinimumAge = "18 Years",
            MaximumAge = "65 Years",
            StartDate = new DateOnly(2024, 1, 1),
            CompletionDate = new DateOnly(2025, 1, 1),
            StudyFirstPostDate = new DateOnly(2023, 6, 1),
            Source = "ClinicalTrials.gov/v2",
            IsIncomplete = false,
            StudyInvestigators =
            [
                new StudyInvestigatorEntity { RoleOnStudy = "PRINCIPAL_INVESTIGATOR" },
                new StudyInvestigatorEntity { RoleOnStudy = "SUB_INVESTIGATOR" }
            ],
            StudyPapers =
            [
                new StudyPaperEntity { PubmedPaperId = Guid.NewGuid() }
            ],
            Conditions =
            [
                new StudyConditionEntity
                {
                    MeshDescriptor = new MeshDescriptorEntity { Name = "Cancer" }
                },
                new StudyConditionEntity
                {
                    MeshDescriptor = new MeshDescriptorEntity { Name = "Heart Disease" }
                }
            ],
            Phases =
            [
                new StudyPhaseEntity { Phase = "Phase 2" },
                new StudyPhaseEntity { Phase = "Phase 3" }
            ],
            Keywords =
            [
                new StudyKeywordEntity { Keyword = "oncology" }
            ],
            Locations =
            [
                new StudyLocationEntity { Facility = "Test Hospital", City = "Boston", State = "MA", Country = "US" }
            ],
            References =
            [
                new StudyReferenceEntity { Pmid = "12345", Citation = "Test Citation", Type = "BACKGROUND" }
            ],
            Outcomes =
            [
                new StudyOutcomeEntity { OutcomeType = "PRIMARY", Measure = "Survival", Description = "OS", TimeFrame = "5 years" }
            ],
            ArmGroups =
            [
                new StudyArmGroupEntity { Label = "Arm 1", Type = "Experimental", Description = "Test arm" }
            ]
        };
    }

    [TestMethod]
    public void ToSummary_MapsAllScalarFields()
    {
        var study = CreateFullStudy();
        dynamic result = StudyMapper.ToSummary(study);

        Assert.AreEqual("NCT001", result.nctId);
        Assert.AreEqual("Test Study", result.briefTitle);
        Assert.AreEqual("RECRUITING", result.overallStatus);
        Assert.AreEqual("Interventional", result.studyType);
        Assert.AreEqual(100, result.enrollmentCount);
        Assert.AreEqual(new DateOnly(2024, 1, 1), result.startDate);
        Assert.AreEqual(new DateOnly(2025, 1, 1), result.completionDate);
        Assert.IsFalse((bool)result.isIncomplete);
    }

    [TestMethod]
    public void ToSummary_DerivedCounts_FromNavigationProperties()
    {
        var study = CreateFullStudy();
        dynamic result = StudyMapper.ToSummary(study);

        Assert.AreEqual(2, result.investigatorCount);
        Assert.AreEqual(1, result.pubmedPaperCount);
    }

    [TestMethod]
    public void ToSummary_Conditions_MapsMeshDescriptorNames()
    {
        var study = CreateFullStudy();
        dynamic result = StudyMapper.ToSummary(study);

        var conditions = (List<string?>)result.conditions;
        Assert.IsNotNull(conditions);
        Assert.AreEqual(2, conditions.Count);
        Assert.AreEqual("Cancer", conditions[0]);
        Assert.AreEqual("Heart Disease", conditions[1]);
    }

    [TestMethod]
    public void ToSummary_ConditionWithNullMeshDescriptor_YieldsNullInList()
    {
        var study = CreateFullStudy();
        study.Conditions = [new StudyConditionEntity { MeshDescriptor = null }];
        dynamic result = StudyMapper.ToSummary(study);

        var conditions = (List<string?>)result.conditions;
        Assert.IsNotNull(conditions);
        Assert.AreEqual(1, conditions.Count);
        Assert.IsNull(conditions[0]);
    }

    [TestMethod]
    public void ToSummary_Phases_MapsPhaseNames()
    {
        var study = CreateFullStudy();
        dynamic result = StudyMapper.ToSummary(study);

        var phases = (List<string?>)result.phases;
        Assert.IsNotNull(phases);
        Assert.AreEqual(2, phases.Count);
        Assert.AreEqual("Phase 2", phases[0]);
        Assert.AreEqual("Phase 3", phases[1]);
    }

    [TestMethod]
    public void ToSummary_NullCollections_DefaultToZero()
    {
        var study = CreateFullStudy();
        study.StudyInvestigators = null;
        study.StudyPapers = null;

        dynamic result = StudyMapper.ToSummary(study);

        Assert.AreEqual(0, result.investigatorCount);
        Assert.AreEqual(0, result.pubmedPaperCount);
    }

    [TestMethod]
    public void ToDetail_MapsAllScalarFields()
    {
        var study = CreateFullStudy();
        dynamic result = StudyMapper.ToDetail(study);

        Assert.AreEqual("NCT001", result.nctId);
        Assert.AreEqual("Test Study", result.briefTitle);
        Assert.AreEqual("A Test Study for Testing", result.officialTitle);
        Assert.AreEqual("RECRUITING", result.overallStatus);
        Assert.AreEqual("Interventional", result.studyType);
        Assert.AreEqual("This is a test summary", result.briefSummary);
        Assert.AreEqual("Treatment", result.primaryPurpose);
        Assert.AreEqual("Parallel", result.interventionModel);
        Assert.AreEqual("Randomized", result.allocation);
        Assert.AreEqual("Double", result.masking);
        Assert.AreEqual("ORG-001", result.orgStudyId);
        Assert.AreEqual("Test Sponsor", result.leadSponsorName);
        Assert.AreEqual("Collab1, Collab2", result.collaboratorNames);
        Assert.AreEqual("Inclusion: healthy", result.eligibilityCriteria);
        Assert.AreEqual("Yes", result.healthyVolunteers);
        Assert.AreEqual(100, result.enrollmentCount);
        Assert.AreEqual("All", result.sex);
        Assert.AreEqual("18 Years", result.minimumAge);
        Assert.AreEqual("65 Years", result.maximumAge);
    }

    [TestMethod]
    public void ToDetail_Dates_Mapped()
    {
        var study = CreateFullStudy();
        dynamic result = StudyMapper.ToDetail(study);

        Assert.AreEqual(new DateOnly(2024, 1, 1), result.startDate);
        Assert.AreEqual(new DateOnly(2025, 1, 1), result.completionDate);
        Assert.AreEqual(new DateOnly(2023, 6, 1), result.studyFirstPostDate);
        Assert.IsFalse((bool)result.isIncomplete);
    }

    [TestMethod]
    public void ToDetail_Investigators_Mapped()
    {
        var study = CreateFullStudy();
        study.StudyInvestigators = [
            new StudyInvestigatorEntity
            {
                RoleOnStudy = "PRINCIPAL_INVESTIGATOR",
                InvestigatorPerson = new InvestigatorPersonEntity
                {
                    FullName = "Dr. Smith",
                    Affiliations = [new InvestigatorAffiliationEntity { InstitutionName = "Harvard", IsPrimary = true }]
                }
            }
        ];
        dynamic result = StudyMapper.ToDetail(study);
        var investigatorCount = 0;
        foreach (dynamic inv in result.investigators)
        {
            investigatorCount++;
            Assert.AreEqual("Dr. Smith", inv.name);
            Assert.AreEqual("PRINCIPAL_INVESTIGATOR", inv.role);
            Assert.AreEqual("Harvard", inv.affiliation);
        }
        Assert.AreEqual(1, investigatorCount);
    }

    [TestMethod]
    public void ToDetail_ConditionsKeywordsPhases_Lists()
    {
        var study = CreateFullStudy();
        dynamic result = StudyMapper.ToDetail(study);

        var conditions = (List<string?>)result.conditions;
        Assert.AreEqual(2, conditions.Count);
        Assert.AreEqual("Cancer", conditions[0]);

        var keywords = (List<string?>)result.keywords;
        Assert.AreEqual(1, keywords.Count);
        Assert.AreEqual("oncology", keywords[0]);

        var phases = (List<string?>)result.phases;
        Assert.AreEqual(2, phases.Count);
    }

    [TestMethod]
    public void ToDetail_Locations_Mapped()
    {
        var study = CreateFullStudy();
        dynamic result = StudyMapper.ToDetail(study);

        var locationCount = 0;
        foreach (dynamic loc in result.locations)
        {
            locationCount++;
            Assert.AreEqual("Test Hospital", loc.facility);
            Assert.AreEqual("Boston", loc.city);
            Assert.AreEqual("MA", loc.state);
            Assert.AreEqual("US", loc.country);
        }
        Assert.AreEqual(1, locationCount);
    }

    [TestMethod]
    public void ToDetail_PubmedPapers_Mapped()
    {
        var pid = Guid.NewGuid();
        var study = CreateFullStudy();
        study.StudyPapers = [
            new StudyPaperEntity
            {
                PubmedPaper = new PubmedPaperEntity
                {
                    Pmid = "12345",
                    Doi = "10.1000/test",
                    Title = "Test Paper",
                    Journal = "Test Journal",
                    PublicationDate = new DateTime(2024, 1, 1),
                    Abstract = "Test abstract",
                    IsNonEnglish = false,
                    PublicationTypes = "Journal Article"
                }
            }
        ];
        dynamic result = StudyMapper.ToDetail(study);

        var paperCount = 0;
        foreach (dynamic paper in result.pubmedPapers)
        {
            paperCount++;
            Assert.AreEqual("12345", paper.pmid);
            Assert.AreEqual("Test Paper", paper.title);
            Assert.AreEqual("Test Journal", paper.journal);
            Assert.IsFalse((bool)paper.isNonEnglish);
        }
        Assert.AreEqual(1, paperCount);
    }

    [TestMethod]
    public void ToDetail_NullCollections_Handled()
    {
        var study = CreateFullStudy();
        study.StudyInvestigators = null;
        study.Conditions = null;
        study.Keywords = null;
        study.Phases = null;
        study.Locations = null;
        study.References = null;
        study.Outcomes = null;
        study.ArmGroups = null;
        study.StudyPapers = null;

        dynamic result = StudyMapper.ToDetail(study);

        Assert.AreEqual("NCT001", result.nctId); // basic field still maps
        Assert.IsNull(result.investigators);
        Assert.IsNull(result.conditions);
        Assert.IsNull(result.keywords);
        Assert.IsNull(result.phases);
        Assert.IsNull(result.locations);
        Assert.IsNull(result.references);
        Assert.IsNull(result.outcomes);
        Assert.IsNull(result.armGroups);
        Assert.IsNull(result.pubmedPapers);
    }

    [TestMethod]
    public void ToPipelineRun_MapsAllFields()
    {
        var started = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var completed = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var run = new PipelineRunEntity
        {
            Id = 42,
            StartedAt = started,
            CompletedAt = completed,
            Status = "Completed",
            TotalStudies = 100,
            TotalInvestigators = 50,
            TotalPubmedPapers = 200,
            TotalKeywords = 300,
            TotalAuthors = 25,
            ErrorMessage = null
        };

        dynamic result = StudyMapper.ToPipelineRun(run);

        Assert.AreEqual(42, result.id);
        Assert.AreEqual(started, result.startedAt);
        Assert.AreEqual(completed, result.completedAt);
        Assert.AreEqual("Completed", result.status);
        Assert.AreEqual(100, result.totalStudies);
        Assert.AreEqual(50, result.totalInvestigators);
        Assert.AreEqual(200, result.totalPubmedPapers);
        Assert.AreEqual(300, result.totalKeywords);
        Assert.AreEqual(25, result.totalAuthors);
        Assert.IsNull(result.errorMessage);
    }

    [TestMethod]
    public void ToPipelineRun_NullableFields_Null()
    {
        var run = new PipelineRunEntity
        {
            Id = 1,
            StartedAt = DateTime.UtcNow,
            Status = "Running",
            CompletedAt = null,
            TotalStudies = null,
            TotalInvestigators = null,
            TotalPubmedPapers = null,
            TotalKeywords = null,
            TotalAuthors = null,
            ErrorMessage = null
        };

        dynamic result = StudyMapper.ToPipelineRun(run);

        Assert.AreEqual(1, result.id);
        Assert.IsNull(result.completedAt);
        Assert.IsNull(result.totalStudies);
        Assert.IsNull(result.totalInvestigators);
        Assert.IsNull(result.totalPubmedPapers);
        Assert.IsNull(result.totalKeywords);
        Assert.IsNull(result.totalAuthors);
        Assert.IsNull(result.errorMessage);
    }
}
