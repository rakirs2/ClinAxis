using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Coordinators;
using Scrapers.Testing;
using Scrapers.Persistence.Entities;
using System;
using System.Linq;

namespace Scrapers.IntegrationTests;

[TestClass]
public class FiftyTrialMappingIntegrationTest : DbTestBase
{
    [TestMethod]
    public async Task Run_50_Trials_And_Report_Counts()
    {
        var originalConnectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
        Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", ConnectionString);
        try
        {
            PipelineResult result = await PipelineRunner.RunAsync(clinicalTrialsCount: 50);

            Console.WriteLine("=== 50-Trial Mapping Results ===");
            Console.WriteLine($"Studies: {result.StudyCount}");
            Console.WriteLine($"Investigators: {result.InvestigatorCount}");
            Console.WriteLine($"PubMed publications: {result.PubmedStudyCount}");
            Console.WriteLine($"Keywords: {await Context.StudyKeywords.CountAsync()}");
            Console.WriteLine($"Conditions: {await Context.StudyConditions.CountAsync()}");
            Console.WriteLine($"Phases: {await Context.StudyPhases.CountAsync()}");

            List<StudyEntity> studies = await Context.Studies
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .OrderBy(s => s.NctId)
                .Take(5)
                .ToListAsync();
            Console.WriteLine();
            Console.WriteLine("First 5 studies with full metadata:");
            foreach (StudyEntity? s in studies)
            {
                Console.WriteLine($"--- {s.NctId} ---");
                Console.WriteLine($"  BriefTitle:     {s.BriefTitle}");
                Console.WriteLine($"  OfficialTitle:  {(s.OfficialTitle?.Length > 80 ? s.OfficialTitle[..80] + "..." : s.OfficialTitle)}");
                Console.WriteLine($"  StudyType:     {s.StudyType}");
                Console.WriteLine($"  Status:         {s.OverallStatus}");
                Console.WriteLine($"  Phase(s):      {string.Join(", ", s.Phases?.Select(p => p.Phase) ?? [])}");
                Console.WriteLine($"  Purpose:       {s.PrimaryPurpose}");
                Console.WriteLine($"  Model:         {s.InterventionModel}");
                Console.WriteLine($"  Allocation:    {s.Allocation}");
                Console.WriteLine($"  Enrollment:    {s.EnrollmentCount}");
                Console.WriteLine($"  Start:         {s.StartDate}");
                Console.WriteLine($"  Completion:    {s.CompletionDate}");
                Console.WriteLine($"  FirstPost:     {s.StudyFirstPostDate}");
                Console.WriteLine($"  Sex:           {s.Sex}");
                Console.WriteLine($"  MinAge:        {s.MinimumAge}");
                Console.WriteLine($"  Conditions:    {string.Join("; ", s.Conditions?.Select(c => c.Condition).Take(3) ?? [])}{(s.Conditions?.Count > 3 ? "..." : "")}");
                Console.WriteLine($"  Keywords:      {string.Join("; ", s.Keywords?.Select(k => k.Keyword).Take(5) ?? [])}{(s.Keywords?.Count > 5 ? "..." : "")}");
                Console.WriteLine();
            }

            Console.WriteLine();

            if (result.Errors != null)
            {
                Console.WriteLine($"Validation errors ({result.Errors.Count}):");
                foreach (ValidationError err in result.Errors)
                {
                    Console.WriteLine($"  [{err.Entity}] {err.Detail}");
                }
            }

            Assert.AreEqual(50, result.StudyCount, "Expected exactly 50 studies.");
            Assert.IsTrue(result.InvestigatorCount > 0, "Expected at least one investigator.");
            Assert.IsNull(result.Errors, "Expected no validation errors.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", originalConnectionString);
        }
    }
}
