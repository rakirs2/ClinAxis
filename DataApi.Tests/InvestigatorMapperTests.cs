using System.Text.Json;
using DataApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;

namespace DataApi.Tests;

[TestClass]
public sealed class InvestigatorMapperTests
{
    private static InvestigatorPersonEntity CreateBaseInvestigator()
    {
        return new InvestigatorPersonEntity
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FullName = "Dr. Smith",
            Orcid = "0000-0001-2345-6789",
            NcbiId = "NCBI123",
            Npi = "1234567890",
            IsHuman = true,
        };
    }

    private static List<StudyEntity> CreateStudiesForInvestigator()
    {
        var study1 = new StudyEntity
        {
            NctId = "NCT001",
            BriefTitle = "Study 1",
            OverallStatus = "COMPLETED",
            StudyInvestigators =
            [
                new StudyInvestigatorEntity
                {
                    InvestigatorPerson = new InvestigatorPersonEntity { FullName = "Dr. Smith" }
                },
                new StudyInvestigatorEntity
                {
                    InvestigatorPerson = new InvestigatorPersonEntity { FullName = "Dr. Jones" }
                }
            ],
            Conditions =
            [
                new StudyConditionEntity { MeshDescriptor = new MeshDescriptorEntity { Name = "Cancer" } }
            ],
            Phases = [new StudyPhaseEntity { Phase = "Phase 2" }]
        };

        var study2 = new StudyEntity
        {
            NctId = "NCT002",
            BriefTitle = "Study 2",
            OverallStatus = "RECRUITING",
            StudyInvestigators =
            [
                new StudyInvestigatorEntity
                {
                    InvestigatorPerson = new InvestigatorPersonEntity { FullName = "Dr. Smith" }
                },
                new StudyInvestigatorEntity
                {
                    InvestigatorPerson = new InvestigatorPersonEntity { FullName = "Dr. Jones" }
                }
            ],
            Conditions =
            [
                new StudyConditionEntity { MeshDescriptor = new MeshDescriptorEntity { Name = "Cancer" } },
                new StudyConditionEntity { MeshDescriptor = new MeshDescriptorEntity { Name = "Heart Disease" } }
            ],
            Phases = [new StudyPhaseEntity { Phase = "Phase 2" }, new StudyPhaseEntity { Phase = "Phase 3" }]
        };

        return [study1, study2];
    }

    [TestMethod]
    public void ToSummary_MapsAllFields()
    {
        var person = CreateBaseInvestigator();
        person.StudyInvestigators =
        [
            new StudyInvestigatorEntity(),
            new StudyInvestigatorEntity()
        ];
        person.InvestigatorPapers =
        [
            new InvestigatorPaperEntity()
        ];
        person.Affiliations =
        [
            new InvestigatorAffiliationEntity { InstitutionName = "Harvard", IsPrimary = false },
            new InvestigatorAffiliationEntity { InstitutionName = "MIT", IsPrimary = true }
        ];

        dynamic result = InvestigatorMapper.ToSummary(person);

        Assert.AreEqual(person.Id, result.uuid);
        Assert.AreEqual("Dr. Smith", result.name);
        Assert.AreEqual("0000-0001-2345-6789", result.orcid);
        Assert.AreEqual("NCBI123", result.ncbiId);
        Assert.AreEqual(2, result.studyCount);
        Assert.AreEqual(1, result.paperCount);
        Assert.AreEqual("MIT", result.primaryAffiliation);
    }

    [TestMethod]
    public void ToSummary_NullCollections_DefaultToZero()
    {
        var person = CreateBaseInvestigator();
        person.StudyInvestigators = null;
        person.InvestigatorPapers = null;
        person.Affiliations = null;

        dynamic result = InvestigatorMapper.ToSummary(person);

        Assert.AreEqual(0, result.studyCount);
        Assert.AreEqual(0, result.paperCount);
        Assert.IsNull(result.primaryAffiliation);
    }

    [TestMethod]
    public void ToSummary_NoPrimaryAffiliation_ReturnsNull()
    {
        var person = CreateBaseInvestigator();
        person.Affiliations =
        [
            new InvestigatorAffiliationEntity { InstitutionName = "Harvard", IsPrimary = false }
        ];

        dynamic result = InvestigatorMapper.ToSummary(person);

        Assert.IsNull(result.primaryAffiliation);
    }

    [TestMethod]
    public void ToDetail_BasicFields()
    {
        var person = CreateBaseInvestigator();
        var studies = CreateStudiesForInvestigator();

        dynamic result = InvestigatorMapper.ToDetail(person, studies);

        Assert.AreEqual(person.Id, result.uuid);
        Assert.AreEqual("Dr. Smith", result.name);
        Assert.AreEqual("0000-0001-2345-6789", result.orcid);
        Assert.AreEqual("NCBI123", result.ncbiId);
        Assert.AreEqual("1234567890", result.npi);
        Assert.AreEqual(2, result.studyCount);
    }

    [TestMethod]
    public void ToDetail_CoInvestigators_Deduplicated()
    {
        var person = CreateBaseInvestigator();
        var studies = CreateStudiesForInvestigator();

        dynamic result = InvestigatorMapper.ToDetail(person, studies);

        var coInvestigators = (List<string>)result.coInvestigators;
        Assert.AreEqual(1, coInvestigators.Count);
        Assert.AreEqual("Dr. Jones", coInvestigators[0]);
    }

    [TestMethod]
    public void ToDetail_Statistics_ByStatus()
    {
        var person = CreateBaseInvestigator();
        var studies = CreateStudiesForInvestigator();

        dynamic result = InvestigatorMapper.ToDetail(person, studies);
        dynamic statistics = result.statistics;
        var byStatus = (Dictionary<string, int>)statistics.byStatus;

        Assert.AreEqual(2, byStatus.Count);
        Assert.AreEqual(1, byStatus["COMPLETED"]);
        Assert.AreEqual(1, byStatus["RECRUITING"]);
    }

    [TestMethod]
    public void ToDetail_Statistics_ByPhase()
    {
        var person = CreateBaseInvestigator();
        var studies = CreateStudiesForInvestigator();

        dynamic result = InvestigatorMapper.ToDetail(person, studies);
        dynamic statistics = result.statistics;
        var byPhase = (Dictionary<string, int>)statistics.byPhase;

        Assert.AreEqual(2, byPhase.Count);
        Assert.AreEqual(2, byPhase["Phase 2"]); // appears in both studies
        Assert.AreEqual(1, byPhase["Phase 3"]);
    }

    [TestMethod]
    public void ToDetail_ConditionsFocusAreas_Deduplicated()
    {
        var person = CreateBaseInvestigator();
        var studies = CreateStudiesForInvestigator();

        dynamic result = InvestigatorMapper.ToDetail(person, studies);

        var conditions = (List<string>)result.conditionsFocusAreas;
        Assert.AreEqual(2, conditions.Count);
        Assert.AreEqual("Cancer", conditions[0]);
        Assert.AreEqual("Heart Disease", conditions[1]);
    }

    [TestMethod]
    public void ToDetail_Affiliations_Mapped()
    {
        var person = CreateBaseInvestigator();
        person.Affiliations =
        [
            new InvestigatorAffiliationEntity
            {
                InstitutionName = "Harvard",
                Department = "Oncology",
                City = "Boston",
                State = "MA",
                Country = "US",
                Role = "Principal Investigator",
                IsPrimary = true
            }
        ];

        dynamic result = InvestigatorMapper.ToDetail(person, []);

        var affiliationCount = 0;
        foreach (dynamic aff in result.affiliations)
        {
            affiliationCount++;
            Assert.AreEqual("Harvard", aff.institution);
            Assert.AreEqual("Oncology", aff.department);
            Assert.AreEqual("Boston", aff.city);
            Assert.AreEqual("MA", aff.state);
            Assert.AreEqual("US", aff.country);
            Assert.IsTrue((bool)aff.isPrimary);
        }
        Assert.AreEqual(1, affiliationCount);
    }

    [TestMethod]
    public void ToDetail_Medicare_LatestYearSelected()
    {
        var person = CreateBaseInvestigator();
        person.MedicareUtilizations =
        [
            new MedicareUtilizationEntity
            {
                DataYear = 2022,
                TotalBeneficiaries = 100,
                TotalServices = 500,
                TotalSubmittedCharges = 100000m,
                TotalMedicarePaymentAmount = 50000m,
                ProviderType = "Individual"
            },
            new MedicareUtilizationEntity
            {
                DataYear = 2023,
                TotalBeneficiaries = 150,
                TotalServices = 600,
                TotalSubmittedCharges = 120000m,
                TotalMedicarePaymentAmount = 60000m,
                ProviderType = "Group"
            }
        ];

        dynamic result = InvestigatorMapper.ToDetail(person, []);

        Assert.IsNotNull(result.medicare);
        Assert.AreEqual(2023, result.medicare.dataYear);
        Assert.AreEqual("Group", result.medicare.providerType);
        Assert.AreEqual(150, result.medicare.totalBeneficiaries);
        Assert.AreEqual(600, result.medicare.totalServices);
    }

    [TestMethod]
    public void ToDetail_Medicare_ChronicConditionsJson_Parsed()
    {
        var person = CreateBaseInvestigator();
        var chronicConditions = new Dictionary<string, decimal?>
        {
            ["Hypertension"] = 25.5m,
            ["Diabetes"] = 15.0m
        };
        person.MedicareUtilizations =
        [
            new MedicareUtilizationEntity
            {
                DataYear = 2023,
                ChronicConditionsJson = JsonSerializer.Serialize(chronicConditions),
                TotalBeneficiaries = 100,
            }
        ];

        dynamic result = InvestigatorMapper.ToDetail(person, []);

        Assert.IsNotNull(result.medicare);
        var conditions = (Dictionary<string, decimal?>)result.medicare.chronicConditions;
        Assert.IsNotNull(conditions);
        Assert.AreEqual(2, conditions.Count);
        Assert.AreEqual(25.5m, conditions["Hypertension"]);
        Assert.AreEqual(15.0m, conditions["Diabetes"]);
    }

    [TestMethod]
    public void ToDetail_Medicare_Procedures_TopByYear()
    {
        var person = CreateBaseInvestigator();
        person.MedicareUtilizations =
        [
            new MedicareUtilizationEntity { DataYear = 2023, TotalBeneficiaries = 100 }
        ];
        person.Procedures =
        [
            new MedicareProcedureEntity
            {
                DataYear = 2023,
                HcpcsCode = "99213",
                HcpcsDescription = "Office visit",
                ServiceCount = 50,
                BeneficiaryCount = 30,
                SubmittedChargeAmount = 5000m,
                MedicarePaymentAmount = 2000m
            },
            new MedicareProcedureEntity
            {
                DataYear = 2022,
                HcpcsCode = "99214",
                ServiceCount = 10
            }
        ];

        dynamic result = InvestigatorMapper.ToDetail(person, []);

        Assert.IsNotNull(result.medicare);
        var procedureCount = 0;
        foreach (dynamic proc in result.medicare.procedures)
        {
            procedureCount++;
            Assert.AreEqual("99213", proc.hcpcsCode);
            Assert.AreEqual(50, proc.serviceCount);
        }
        Assert.AreEqual(1, procedureCount); // only 2023 procedures
    }

    [TestMethod]
    public void ToDetail_OpenPayments_Aggregated()
    {
        var person = CreateBaseInvestigator();
        person.OpenPayments =
        [
            new OpenPaymentEntity
            {
                DataYear = 2023,
                PaymentType = "Research",
                PaymentAmount = 10000m,
                PayorName = "Pfizer"
            },
            new OpenPaymentEntity
            {
                DataYear = 2023,
                PaymentType = "Research",
                PaymentAmount = 5000m,
                PayorName = "Pfizer"
            },
            new OpenPaymentEntity
            {
                DataYear = 2023,
                PaymentType = "Travel",
                PaymentAmount = 2000m,
                PayorName = "Merck"
            }
        ];

        dynamic result = InvestigatorMapper.ToDetail(person, []);

        Assert.IsNotNull(result.openPayments);
        dynamic summary = result.openPayments.summary;
        Assert.AreEqual(17000m, summary.totalAmount);
        Assert.AreEqual(3, summary.totalCount);
        Assert.AreEqual(2, summary.uniquePayorCount);

        var typeCount = 0;
        foreach (dynamic kvp in summary.byType)
        {
            typeCount++;
            if (kvp.Key == "Research")
                Assert.AreEqual(15000m, kvp.Value.totalPayments);
            if (kvp.Key == "Travel")
                Assert.AreEqual(2000m, kvp.Value.totalPayments);
        }
        Assert.AreEqual(2, typeCount);
    }

    [TestMethod]
    public void ToDetail_Metrics_SemanticScholar_Selected()
    {
        var person = CreateBaseInvestigator();
        person.Metrics =
        [
            new InvestigatorMetricEntity
            {
                Source = "SemanticScholar",
                HIndex = 25,
                CitationCount = 5000,
                I10Index = 40,
                TotalPapers = 60,
                ExternalAuthorId = "A12345",
                LookupAttemptedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LookupResult = "found"
            },
            new InvestigatorMetricEntity
            {
                Source = "OpenAlex",
                HIndex = 20,
            }
        ];

        dynamic result = InvestigatorMapper.ToDetail(person, []);

        Assert.IsNotNull(result.metrics);
        Assert.AreEqual("SemanticScholar", result.metrics.source);
        Assert.AreEqual(25, result.metrics.hIndex);
        Assert.AreEqual(5000, result.metrics.citationCount);
        Assert.AreEqual(40, result.metrics.i10Index);
        Assert.AreEqual(60, result.metrics.totalPapers);
        Assert.AreEqual("A12345", result.metrics.externalAuthorId);
        Assert.AreEqual("found", result.metrics.lookupResult);
    }

    [TestMethod]
    public void ToDetail_NoEnrichments_AllNull()
    {
        var person = CreateBaseInvestigator();
        person.MedicareUtilizations = null;
        person.OpenPayments = null;
        person.Metrics = null;
        person.Procedures = null;

        dynamic result = InvestigatorMapper.ToDetail(person, []);

        Assert.IsNull(result.medicare);
        Assert.IsNull(result.openPayments);
        Assert.IsNull(result.metrics);
        Assert.AreEqual(0, result.studyCount);
    }

    [TestMethod]
    public void ToDetail_EmptyStudiesList_Graceful()
    {
        var person = CreateBaseInvestigator();

        dynamic result = InvestigatorMapper.ToDetail(person, []);

        Assert.AreEqual(0, result.studyCount);
        Assert.AreEqual(0, ((List<string>)result.coInvestigators).Count);
        Assert.AreEqual(0, ((List<string>)result.conditionsFocusAreas).Count);
        dynamic statistics = result.statistics;
        Assert.AreEqual(0, ((Dictionary<string, int>)statistics.byStatus).Count);
        Assert.AreEqual(0, ((Dictionary<string, int>)statistics.byPhase).Count);
    }

    [TestMethod]
    public void ToDetail_EmptyAffiliations_ReturnsEmptyList()
    {
        var person = CreateBaseInvestigator();
        person.Affiliations = [];

        dynamic result = InvestigatorMapper.ToDetail(person, []);

        var affiliationCount = 0;
        foreach (dynamic _ in result.affiliations)
            affiliationCount++;
        Assert.AreEqual(0, affiliationCount);
    }

    [TestMethod]
    public void ToDetail_NullAffiliations_ReturnsNull()
    {
        var person = CreateBaseInvestigator();
        person.Affiliations = null;

        dynamic result = InvestigatorMapper.ToDetail(person, []);

        Assert.IsNull(result.affiliations);
    }
}
