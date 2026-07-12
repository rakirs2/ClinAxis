using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.Testing;

internal static class SeedData
{
    internal static readonly StudyEntity Study1 = new()
    {
        NctId = "NCT00000001",
        BriefTitle = "Test Study Alpha",
        OverallStatus = "ACTIVE"
    };

    internal static readonly InvestigatorEntity Investigator1 = new()
    {
        StudyNctId = "NCT00000002",
        Name = "Dr. Alice Johnson, MD",
        Affiliation = "Pain Research Institute",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator2 = new()
    {
        StudyNctId = "NCT00000002",
        Name = "Dr. Bob Williams, PhD",
        Affiliation = "Pain Research Institute",
        Role = "SUB_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator3 = new()
    {
        StudyNctId = "NCT00000003",
        Name = "Dr. Alice Johnson, MSc",
        Affiliation = "Cardio Health Center",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator4 = new()
    {
        StudyNctId = "NCT00000004",
        Name = "Dr. Carol Davis, MD",
        Affiliation = "Asthma Care Institute",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator5 = new()
    {
        StudyNctId = "NCT00000005",
        Name = "Dr. Anna Smith, MD",
        Affiliation = "Cancer Research Center",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator6 = new()
    {
        StudyNctId = "NCT00000006",
        Name = "Dr. Campbell Lee, PhD",
        Affiliation = "Diabetes Institute",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator7 = new()
    {
        StudyNctId = "NCT00000007",
        Name = "Dr. Joshua Wright, MD, MSc",
        Affiliation = "Depression Research Lab",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator8 = new()
    {
        StudyNctId = "NCT00000008",
        Name = "Prof. Michael Johnson, MD",
        Affiliation = "Neurology Institute",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator9 = new()
    {
        StudyNctId = "NCT00000009",
        Name = "Prof. Dr. Patricia Williams, PhD",
        Affiliation = "Hypertension Research Center",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator10 = new()
    {
        StudyNctId = "NCT00000010",
        Name = "Dr. Robert Davis, MSc",
        Affiliation = "Genetic Disorder Institute",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator11 = new()
    {
        StudyNctId = "NCT00000011",
        Name = "Prof. Susan Miller, DM",
        Affiliation = "Multi-Site Research Center",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator12 = new()
    {
        StudyNctId = "NCT00000002",
        Name = "Dr. Emma Thompson, MD",
        Affiliation = "Pain Research Institute",
        Role = "SUB_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator13 = new()
    {
        StudyNctId = "NCT00000003",
        Name = "Dr. Frank Rodriguez, PhD",
        Affiliation = "Cardio Health Center",
        Role = "SUB_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator14 = new()
    {
        StudyNctId = "NCT00000011",
        Name = "Dr. Grace Chen, MD, PhD",
        Affiliation = "Multi-Site Research Center",
        Role = "SUB_INVESTIGATOR"
    };

    internal static readonly StudyEntity Study2 = new()
    {
        NctId = "NCT00000002",
        BriefTitle = "Pregabalin for Neuropathic Pain Relief Trial",
        OverallStatus = "COMPLETED",
        EnrollmentCount = 200,
        Conditions = [new StudyConditionEntity { Condition = "Neuropathic Pain" }],
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
            new StudyConditionEntity { Condition = "Cardiovascular Diseases" },
            new StudyConditionEntity { Condition = "Heart Disease" }
        ],
        Phases = [new StudyPhaseEntity { Phase = "PHASE2" }]
    };

    internal static readonly StudyEntity Study4 = new()
    {
        NctId = "NCT00000004",
        BriefTitle = "Pediatric Asthma Treatment Study",
        OverallStatus = "RECRUITING",
        EnrollmentCount = 150,
        Conditions = [new StudyConditionEntity { Condition = "Asthma" }],
        Phases = [new StudyPhaseEntity { Phase = "PHASE4" }]
    };

    internal static readonly StudyEntity Study5 = new()
    {
        NctId = "NCT00000005",
        BriefTitle = "Cancer Immunotherapy Trial",
        OverallStatus = "TERMINATED",
        EnrollmentCount = 50,
        Conditions = [new StudyConditionEntity { Condition = "Melanoma" }],
        Phases = [new StudyPhaseEntity { Phase = "PHASE1" }]
    };

    internal static readonly StudyEntity Study6 = new()
    {
        NctId = "NCT00000006",
        BriefTitle = "Diabetes Type 2 Management Study",
        OverallStatus = "RECRUITING",
        EnrollmentCount = 300,
        StartDate = new DateOnly(2024, 1, 15),
        Conditions = [new StudyConditionEntity { Condition = "Type 2 Diabetes" }],
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
        Conditions = [new StudyConditionEntity { Condition = "Major Depression" }],
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
            new StudyConditionEntity { Condition = "Alzheimer's Disease" },
            new StudyConditionEntity { Condition = "Cognitive Impairment" }
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
        Conditions = [new StudyConditionEntity { Condition = "Hypertension" }],
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
        Conditions = [new StudyConditionEntity { Condition = "Rare Genetic Disorder" }],
        Phases = [new StudyPhaseEntity { Phase = "PHASE1" }],
        Locations =
        [
            new StudyLocationEntity { Country = "United States", State = "TX", City = "Houston", Facility = "Texas Medical Center" }
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
            new StudyConditionEntity { Condition = "Hypertension" },
            new StudyConditionEntity { Condition = "Type 2 Diabetes" }
        ],
        Phases = [new StudyPhaseEntity { Phase = "PHASE3" }],
        Locations =
        [
            new StudyLocationEntity { Country = "United States", State = "NY", City = "New York", Facility = "Cornell Weill Medical" },
            new StudyLocationEntity { Country = "United States", State = "CA", City = "San Diego", Facility = "UCSD Medical Center" }
        ]
    };

    internal static async Task SeedAsync(ClinicalTrialsContext ctx)
    {
        // Assign UUIDs to all investigators (required for detail page routing)
        Investigator1.Uuid = new Guid("11111111-1111-1111-1111-111111111111");
        Investigator2.Uuid = new Guid("22222222-2222-2222-2222-222222222222");
        Investigator3.Uuid = new Guid("33333333-3333-3333-3333-333333333333");
        Investigator4.Uuid = new Guid("44444444-4444-4444-4444-444444444444");
        Investigator5.Uuid = new Guid("55555555-5555-5555-5555-555555555555");
        Investigator6.Uuid = new Guid("66666666-6666-6666-6666-666666666666");
        Investigator7.Uuid = new Guid("77777777-7777-7777-7777-777777777777");
        Investigator8.Uuid = new Guid("88888888-8888-8888-8888-888888888888");
        Investigator9.Uuid = new Guid("99999999-9999-9999-9999-999999999999");
        Investigator10.Uuid = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Investigator11.Uuid = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Investigator12.Uuid = new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc");
        Investigator13.Uuid = new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd");
        Investigator14.Uuid = new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        ctx.Studies.AddRange(Study1, Study2, Study3, Study4, Study5, Study6, Study7, Study8, Study9, Study10, Study11);
        ctx.Investigators.AddRange(Investigator1, Investigator2, Investigator3, Investigator4, 
                                   Investigator5, Investigator6, Investigator7, Investigator8,
                                   Investigator9, Investigator10, Investigator11, Investigator12,
                                   Investigator13, Investigator14);
        await ctx.SaveChangesAsync().ConfigureAwait(false);
    }
}
