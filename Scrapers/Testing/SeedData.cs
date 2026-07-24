using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.Testing;

internal static class SeedData
{
    internal static readonly List<MeshDescriptorEntity> TestDescriptors =
    [
        new MeshDescriptorEntity { Id = 1001, Cui = "D009765", Name = "Neuropathic Pain", TreeNumbers = ["C10.500"], Category = "disease" },
        new MeshDescriptorEntity { Id = 1002, Cui = "D002318", Name = "Cardiovascular Diseases", TreeNumbers = ["C14.280"], Category = "disease" },
        new MeshDescriptorEntity { Id = 1003, Cui = "D006333", Name = "Heart Disease", TreeNumbers = ["C14.280"], Category = "disease" },
        new MeshDescriptorEntity { Id = 1004, Cui = "D001249", Name = "Asthma", TreeNumbers = ["C08.127"], Category = "disease" },
        new MeshDescriptorEntity { Id = 1005, Cui = "D008545", Name = "Melanoma", TreeNumbers = ["C04.557"], Category = "disease" },
        new MeshDescriptorEntity { Id = 1006, Cui = "D003924", Name = "Diabetes Mellitus, Type 2", TreeNumbers = ["C19.246"], Category = "disease" },
        new MeshDescriptorEntity { Id = 1007, Cui = "D003865", Name = "Depressive Disorder, Major", TreeNumbers = ["F03.600"], Category = "psychiatry" },
        new MeshDescriptorEntity { Id = 1008, Cui = "D000544", Name = "Alzheimer Disease", TreeNumbers = ["C10.228"], Category = "disease" },
        new MeshDescriptorEntity { Id = 1009, Cui = "D003072", Name = "Cognitive Impairment", TreeNumbers = ["F01.058"], Category = "psychiatry" },
        new MeshDescriptorEntity { Id = 1010, Cui = "D006973", Name = "Hypertension", TreeNumbers = ["C14.907"], Category = "disease" },
        new MeshDescriptorEntity { Id = 1011, Cui = "D030342", Name = "Genetic Diseases, Inborn", TreeNumbers = ["C16.320"], Category = "disease" },
        new MeshDescriptorEntity { Id = 1012, Cui = "D015415", Name = "Biomarkers, Pharmacological", TreeNumbers = ["D23.101"], Category = "chemical" },
    ];

    internal static readonly StudyEntity Study1 = new()
    {
        NctId = "NCT00000001",
        BriefTitle = "Test Study Alpha",
        OverallStatus = "ACTIVE"
    };

    internal static readonly StudyEntity Study2 = new()
    {
        NctId = "NCT00000002",
        BriefTitle = "Pregabalin for Neuropathic Pain Relief Trial",
        OverallStatus = "COMPLETED",
        EnrollmentCount = 200,
        Conditions = [new StudyConditionEntity { MeshDescriptorId = 1001 }],
        Phases = [new StudyPhaseEntity { Phase = "PHASE3" }]
    };

    internal static readonly StudyEntity Study3 = new()
    {
        NctId = "NCT00000003",
        BriefTitle = "Aspirin Cardiovascular Prevention Study",
        OverallStatus = "RECRUITING",
        EnrollmentCount = 5000,
        Conditions =
        [
            new StudyConditionEntity { MeshDescriptorId = 1002 },
            new StudyConditionEntity { MeshDescriptorId = 1003 }
        ],
        Phases = [new StudyPhaseEntity { Phase = "PHASE2" }]
    };

    internal static readonly StudyEntity Study4 = new()
    {
        NctId = "NCT00000004",
        BriefTitle = "Pediatric Asthma Treatment Study",
        OverallStatus = "RECRUITING",
        EnrollmentCount = 150,
        Conditions = [new StudyConditionEntity { MeshDescriptorId = 1004 }],
        Phases = [new StudyPhaseEntity { Phase = "PHASE4" }]
    };

    internal static readonly StudyEntity Study5 = new()
    {
        NctId = "NCT00000005",
        BriefTitle = "Cancer Immunotherapy Trial",
        OverallStatus = "TERMINATED",
        EnrollmentCount = 50,
        Conditions = [new StudyConditionEntity { MeshDescriptorId = 1005 }],
        Phases = [new StudyPhaseEntity { Phase = "PHASE1" }]
    };

    internal static readonly StudyEntity Study6 = new()
    {
        NctId = "NCT00000006",
        BriefTitle = "Diabetes Type 2 Management Study",
        OverallStatus = "RECRUITING",
        EnrollmentCount = 300,
        StartDate = new DateOnly(2024, 1, 15),
        Conditions = [new StudyConditionEntity { MeshDescriptorId = 1006 }],
        Phases = [new StudyPhaseEntity { Phase = "PHASE3" }],
        Locations =
        [
            new StudyLocationEntity { Country = "United States", State = "CA", City = "San Francisco", Facility = "UCSF Medical Center" },
            new StudyLocationEntity { Country = "United States", State = "NY", City = "New York", Facility = "Columbia Presbyterian" }
        ]
    };

    internal static readonly StudyEntity Study7 = new()
    {
        NctId = "NCT00000007",
        BriefTitle = "Depression Treatment Trial - Germany",
        OverallStatus = "ACTIVE",
        EnrollmentCount = 120,
        StartDate = new DateOnly(2023, 6, 1),
        Conditions = [new StudyConditionEntity { MeshDescriptorId = 1007 }],
        Phases = [new StudyPhaseEntity { Phase = "PHASE2" }],
        Locations =
        [
            new StudyLocationEntity { Country = "Germany", State = "Berlin", City = "Berlin", Facility = "Charité Hospital" }
        ]
    };

    internal static readonly StudyEntity Study8 = new()
    {
        NctId = "NCT00000008",
        BriefTitle = "Alzheimer's Disease Biomarker Study",
        OverallStatus = "RECRUITING",
        EnrollmentCount = 500,
        StartDate = new DateOnly(2024, 3, 10),
        Conditions = 
        [
            new StudyConditionEntity { MeshDescriptorId = 1001 },
            new StudyConditionEntity { MeshDescriptorId = 1009 }
        ],
        Phases = [new StudyPhaseEntity { Phase = "PHASE3" }],
        Locations =
        [
            new StudyLocationEntity { Country = "United States", State = "MA", City = "Boston", Facility = "Massachusetts General Hospital" },
            new StudyLocationEntity { Country = "United States", State = "CA", City = "Los Angeles", Facility = "UCLA" },
            new StudyLocationEntity { Country = "United States", State = "IL", City = "Chicago", Facility = "Northwestern University" }
        ]
    };

    internal static readonly StudyEntity Study9 = new()
    {
        NctId = "NCT00000009",
        BriefTitle = "Hypertension Management in Canada",
        OverallStatus = "COMPLETED",
        EnrollmentCount = 250,
        StartDate = new DateOnly(2022, 9, 1),
        Conditions = [new StudyConditionEntity { MeshDescriptorId = 1010 }],
        Phases = [new StudyPhaseEntity { Phase = "PHASE4" }],
        Locations =
        [
            new StudyLocationEntity { Country = "Canada", State = "Ontario", City = "Toronto", Facility = "Toronto General Hospital" },
            new StudyLocationEntity { Country = "Canada", State = "British Columbia", City = "Vancouver", Facility = "Vancouver Hospital" }
        ]
    };

    internal static readonly StudyEntity Study10 = new()
    {
        NctId = "NCT00000010",
        BriefTitle = "Small Enrollment Low Phase Study",
        OverallStatus = "NOT_YET_RECRUITING",
        EnrollmentCount = 20,
        StartDate = new DateOnly(2025, 1, 1),
        Conditions = [new StudyConditionEntity { MeshDescriptorId = 1011 }],
        Phases = [new StudyPhaseEntity { Phase = "PHASE1" }],
        Locations =
        [
            new StudyLocationEntity { Country = "United States", State = "TX", City = "Houston", Facility = "Texas Medical Center" }
        ]
    };

    internal static readonly StudyEntity Study12 = new()
    {
        NctId = "NCT00000012",
        BriefTitle = "Observational Biomarker Discovery Study",
        OverallStatus = "RECRUITING",
        EnrollmentCount = 500,
        StartDate = new DateOnly(2024, 6, 1),
        Conditions = [new StudyConditionEntity { MeshDescriptorId = 1012 }],
        Phases = [new StudyPhaseEntity { Phase = "NA" }],
        Locations =
        [
            new StudyLocationEntity { Country = "United States", State = "CA", City = "Stanford", Facility = "Stanford Medical Center" }
        ]
    };

    internal static readonly StudyEntity Study11 = new()
    {
        NctId = "NCT00000011",
        BriefTitle = "Large Enrollment Phase 3 Multi-Site Study",
        OverallStatus = "RECRUITING",
        EnrollmentCount = 5000,
        StartDate = new DateOnly(2023, 1, 15),
        Conditions =
        [
            new StudyConditionEntity { MeshDescriptorId = 1010 },
            new StudyConditionEntity { MeshDescriptorId = 1006 }
        ],
        Phases = [new StudyPhaseEntity { Phase = "PHASE3" }],
        Locations =
        [
            new StudyLocationEntity { Country = "United States", State = "NY", City = "New York", Facility = "Cornell Weill Medical" },
            new StudyLocationEntity { Country = "United States", State = "CA", City = "San Diego", Facility = "UCSD Medical Center" }
        ]
    };

    internal static readonly InvestigatorPersonEntity Person1 = new()
    {
        Id = Guid.Parse("A0000000-0000-0000-0000-000000000001"),
        FullName = "Dr. Alice Johnson, MD, PhD",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    internal static readonly InvestigatorPersonEntity Person2 = new()
    {
        Id = Guid.Parse("A0000000-0000-0000-0000-000000000002"),
        FullName = "Prof. Bob Williams, MD",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    internal static readonly InvestigatorPersonEntity Person3 = new()
    {
        Id = Guid.Parse("A0000000-0000-0000-0000-000000000003"),
        FullName = "Prof. Carol Davis, PhD",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    internal static readonly InvestigatorPersonEntity Person4 = new()
    {
        Id = Guid.Parse("A0000000-0000-0000-0000-000000000004"),
        FullName = "Dr. James Smith, MD",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    internal static readonly InvestigatorPersonEntity Person5 = new()
    {
        Id = Guid.Parse("A0000000-0000-0000-0000-000000000005"),
        FullName = "Prof. Dr. Sarah Campbell, MSc",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    internal static readonly InvestigatorPersonEntity Person6 = new()
    {
        Id = Guid.Parse("A0000000-0000-0000-0000-000000000006"),
        FullName = "Dr. Michael Chen, PhD",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    internal static readonly InvestigatorPersonEntity Person7 = new()
    {
        Id = Guid.Parse("A0000000-0000-0000-0000-000000000007"),
        FullName = "Prof. Robert Miller, MD, MSc",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    internal static readonly InvestigatorPersonEntity Person8 = new()
    {
        Id = Guid.Parse("A0000000-0000-0000-0000-000000000008"),
        FullName = "Dr. Anna Martinez, PhD",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    internal static readonly InvestigatorPersonEntity Person9 = new()
    {
        Id = Guid.Parse("A0000000-0000-0000-0000-000000000009"),
        FullName = "Dr. David Williams, MD",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    internal static readonly InvestigatorPersonEntity Person10 = new()
    {
        Id = Guid.Parse("A0000000-0000-0000-0000-000000000010"),
        FullName = "Prof. Emily Brown, MD, PhD",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    internal static readonly InvestigatorPersonEntity Person11 = new()
    {
        Id = Guid.Parse("A0000000-0000-0000-0000-000000000011"),
        FullName = "Dr. Christopher Lee, MSc",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    internal static readonly InvestigatorPersonEntity Person12 = new()
    {
        Id = Guid.Parse("A0000000-0000-0000-0000-000000000012"),
        FullName = "Prof. Jennifer Garcia, MD, MSc",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    // Note: Dr. Alice Johnson appears in two studies (NCT00000002 and NCT00000003)
    // Person1 is reused for both, demonstrating the dedup model.
    internal static readonly StudyInvestigatorEntity StudyInvestigator1 = new()
    {
        StudyNctId = "NCT00000002", InvestigatorPersonId = Person1.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true
    };
    internal static readonly StudyInvestigatorEntity StudyInvestigator2 = new()
    {
        StudyNctId = "NCT00000002", InvestigatorPersonId = Person2.Id, RoleOnStudy = "SUB_INVESTIGATOR", IsOverallOfficial = true
    };
    internal static readonly StudyInvestigatorEntity StudyInvestigator3 = new()
    {
        StudyNctId = "NCT00000003", InvestigatorPersonId = Person1.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true
    };
    internal static readonly StudyInvestigatorEntity StudyInvestigator4 = new()
    {
        StudyNctId = "NCT00000004", InvestigatorPersonId = Person3.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true
    };
    internal static readonly StudyInvestigatorEntity StudyInvestigator5 = new()
    {
        StudyNctId = "NCT00000006", InvestigatorPersonId = Person4.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true
    };
    internal static readonly StudyInvestigatorEntity StudyInvestigator6 = new()
    {
        StudyNctId = "NCT00000006", InvestigatorPersonId = Person5.Id, RoleOnStudy = "SUB_INVESTIGATOR", IsOverallOfficial = true
    };
    internal static readonly StudyInvestigatorEntity StudyInvestigator7 = new()
    {
        StudyNctId = "NCT00000007", InvestigatorPersonId = Person6.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true
    };
    internal static readonly StudyInvestigatorEntity StudyInvestigator8 = new()
    {
        StudyNctId = "NCT00000008", InvestigatorPersonId = Person7.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true
    };
    internal static readonly StudyInvestigatorEntity StudyInvestigator9 = new()
    {
        StudyNctId = "NCT00000008", InvestigatorPersonId = Person8.Id, RoleOnStudy = "SUB_INVESTIGATOR", IsOverallOfficial = true
    };
    internal static readonly StudyInvestigatorEntity StudyInvestigator10 = new()
    {
        StudyNctId = "NCT00000009", InvestigatorPersonId = Person9.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true
    };
    internal static readonly StudyInvestigatorEntity StudyInvestigator11 = new()
    {
        StudyNctId = "NCT00000010", InvestigatorPersonId = Person10.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true
    };
    internal static readonly StudyInvestigatorEntity StudyInvestigator12 = new()
    {
        StudyNctId = "NCT00000011", InvestigatorPersonId = Person11.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true
    };
    internal static readonly StudyInvestigatorEntity StudyInvestigator13 = new()
    {
        StudyNctId = "NCT00000011", InvestigatorPersonId = Person12.Id, RoleOnStudy = "SUB_INVESTIGATOR", IsOverallOfficial = true
    };

    internal static readonly PubmedPaperEntity Paper1 = new()
    {
        Id = Guid.Parse("B0000000-0000-0000-0000-000000000001"),
        Pmid = "98765432",
        Doi = "10.1000/abc123",
        Title = "Sample PubMed Publication Alpha",
        Journal = "Test Journal",
        PublicationDate = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc),
        Abstract = "This is a sample abstract for testing.",
        IsNonEnglish = false,
        PublicationTypes = "Journal Article",
    };

    internal static readonly PubmedPaperEntity Paper2 = new()
    {
        Id = Guid.Parse("B0000000-0000-0000-0000-000000000002"),
        Pmid = "98765433",
        Doi = "10.1000/def456",
        Title = "Sample PubMed Publication Beta",
        Journal = "Another Journal",
        PublicationDate = new DateTime(2024, 1, 20, 0, 0, 0, DateTimeKind.Utc),
        Abstract = "Another sample abstract for integration testing.",
        IsNonEnglish = false,
        PublicationTypes = "Clinical Trial",
    };

    internal static readonly StudyPaperEntity StudyPaper1 = new()
    {
        StudyNctId = Study2.NctId, PubmedPaperId = Paper1.Id
    };

    internal static readonly StudyPaperEntity StudyPaper2 = new()
    {
        StudyNctId = Study2.NctId, PubmedPaperId = Paper2.Id
    };

    internal static readonly StudyPaperEntity StudyPaper3 = new()
    {
        StudyNctId = Study3.NctId, PubmedPaperId = Paper1.Id
    };

    internal static readonly InvestigatorPaperEntity InvestigatorPaper1 = new()
    {
        InvestigatorPersonId = Person1.Id, PubmedPaperId = Paper1.Id, AuthorPosition = 1, IsCorrespondingAuthor = true
    };

    internal static readonly InvestigatorPaperEntity InvestigatorPaper2 = new()
    {
        InvestigatorPersonId = Person1.Id, PubmedPaperId = Paper2.Id, AuthorPosition = 2, IsCorrespondingAuthor = false
    };

    internal static readonly InvestigatorPaperEntity InvestigatorPaper3 = new()
    {
        InvestigatorPersonId = Person3.Id, PubmedPaperId = Paper1.Id, AuthorPosition = 3, IsCorrespondingAuthor = false
    };

    internal static async Task SeedAsync(ClinicalTrialsContext ctx)
    {
        ctx.MeshDescriptors.AddRange(TestDescriptors);
        ctx.InvestigatorPersons.AddRange(
            Person1, Person2, Person3, Person4, Person5, Person6,
            Person7, Person8, Person9, Person10, Person11, Person12);
        ctx.Studies.AddRange(Study1, Study2, Study3, Study4, Study5, Study6, Study7, Study8, Study9, Study10, Study11, Study12);
        ctx.StudyInvestigators.AddRange(
            StudyInvestigator1, StudyInvestigator2, StudyInvestigator3, StudyInvestigator4,
            StudyInvestigator5, StudyInvestigator6, StudyInvestigator7, StudyInvestigator8,
            StudyInvestigator9, StudyInvestigator10, StudyInvestigator11, StudyInvestigator12,
            StudyInvestigator13);
        ctx.PubmedPapers.AddRange(Paper1, Paper2);
        ctx.StudyPapers.AddRange(StudyPaper1, StudyPaper2, StudyPaper3);
        ctx.InvestigatorPapers.AddRange(InvestigatorPaper1, InvestigatorPaper2, InvestigatorPaper3);
        await ctx.SaveChangesAsync().ConfigureAwait(false);
    }
}
