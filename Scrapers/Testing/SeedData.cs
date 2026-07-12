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
        Name = "Dr. Alice Johnson, MD, PhD",
        Affiliation = "Pain Research Institute",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator2 = new()
    {
        StudyNctId = "NCT00000002",
        Name = "Prof. Bob Williams, MD",
        Affiliation = "Pain Research Institute",
        Role = "SUB_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator3 = new()
    {
        StudyNctId = "NCT00000003",
        Name = "Dr. Alice Johnson, MD, PhD",
        Affiliation = "Cardio Health Center",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator4 = new()
    {
        StudyNctId = "NCT00000004",
        Name = "Prof. Carol Davis, PhD",
        Affiliation = "Asthma Care Institute",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    // Additional investigators for pagination and search tests
    internal static readonly InvestigatorEntity Investigator5 = new()
    {
        StudyNctId = "NCT00000006",
        Name = "Dr. James Smith, MD",
        Affiliation = "Diabetes Research Center",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator6 = new()
    {
        StudyNctId = "NCT00000006",
        Name = "Prof. Dr. Sarah Campbell, MSc",
        Affiliation = "Diabetes Research Center",
        Role = "SUB_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator7 = new()
    {
        StudyNctId = "NCT00000007",
        Name = "Dr. Michael Chen, PhD",
        Affiliation = "Berlin Mental Health Institute",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator8 = new()
    {
        StudyNctId = "NCT00000008",
        Name = "Prof. Robert Miller, MD, MSc",
        Affiliation = "Neurology Research Center",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator9 = new()
    {
        StudyNctId = "NCT00000008",
        Name = "Dr. Anna Martinez, PhD",
        Affiliation = "Neurology Research Center",
        Role = "SUB_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator10 = new()
    {
        StudyNctId = "NCT00000009",
        Name = "Dr. David Williams, MD",
        Affiliation = "Canadian Cardiovascular Institute",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator11 = new()
    {
        StudyNctId = "NCT00000010",
        Name = "Prof. Emily Brown, MD, PhD",
        Affiliation = "Genetic Disorder Research Center",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator12 = new()
    {
        StudyNctId = "NCT00000011",
        Name = "Dr. Christopher Lee, MSc",
        Affiliation = "Multi-Site Research Network",
        Role = "PRINCIPAL_INVESTIGATOR"
    };

    internal static readonly InvestigatorEntity Investigator13 = new()
    {
        StudyNctId = "NCT00000011",
        Name = "Prof. Jennifer Garcia, MD, MSc",
        Affiliation = "Multi-Site Research Network",
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
        ctx.Studies.AddRange(Study1, Study2, Study3, Study4, Study5, Study6, Study7, Study8, Study9, Study10, Study11);
        ctx.Investigators.AddRange(
            Investigator1, Investigator2, Investigator3, Investigator4, Investigator5, Investigator6,
            Investigator7, Investigator8, Investigator9, Investigator10, Investigator11, Investigator12,
            Investigator13);
        await ctx.SaveChangesAsync().ConfigureAwait(false);
    }
}
