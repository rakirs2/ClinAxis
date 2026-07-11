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

    internal static async Task SeedAsync(ClinicalTrialsContext ctx)
    {
        ctx.Studies.AddRange(Study1, Study2, Study3, Study4, Study5);
        await ctx.SaveChangesAsync();
    }
}
