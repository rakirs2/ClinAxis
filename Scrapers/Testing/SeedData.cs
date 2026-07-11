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

    internal static async Task SeedAsync(ClinicalTrialsContext ctx)
    {
        ctx.Studies.AddRange(Study1);
        await ctx.SaveChangesAsync();
    }
}
