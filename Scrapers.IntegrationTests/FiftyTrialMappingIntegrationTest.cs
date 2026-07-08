using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Coordinators;
using Scrapers.IntegrationTests.Utilities;
using System;
using System.Linq;

namespace Scrapers.IntegrationTests;

[TestClass]
public class FiftyTrialMappingIntegrationTest : EphemeralDbTestBase
{
    [TestMethod]
    public async Task Run_50_Trials_And_Report_Counts()
    {
        var originalConnectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
        Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", ConnectionString);
        try
        {
            var result = await PipelineRunner.RunAsync(clinicalTrialsCount: 50);

            Console.WriteLine("=== 50-Trial Mapping Results ===");
            Console.WriteLine($"Studies: {result.StudyCount}");
            Console.WriteLine($"Investigators: {result.InvestigatorCount}");
            Console.WriteLine($"PubMed publications: {result.PubmedStudyCount}");
            Console.WriteLine($"Keywords: {await Context.StudyKeywords.CountAsync()}");
            Console.WriteLine($"Conditions: {await Context.StudyConditions.CountAsync()}");
            Console.WriteLine($"Phases: {await Context.StudyPhases.CountAsync()}");
            Console.WriteLine($"Authors (flattened): {await Context.StudyAuthors.CountAsync()}");
            Console.WriteLine($"Authors with ORCID: {await Context.StudyAuthors.CountAsync(a => a.Orcid != null)}");

            var studies = await Context.Studies
                .Include(s => s.Keywords)
                .Include(s => s.Conditions)
                .Include(s => s.Phases)
                .OrderBy(s => s.NctId)
                .Take(5)
                .ToListAsync();
            Console.WriteLine();
            Console.WriteLine("First 5 studies with full metadata:");
            foreach (var s in studies)
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

            Console.WriteLine("Sample authors from study_authors (first 15):");
            Console.WriteLine($"{"Id",-5} {"StudyNctId",-16} {"Pmid",-10} {"LastName",-25} {"ForeName",-25} {"Orcid",-25}");
            Console.WriteLine(new string('-', 106));
            var authors = await Context.StudyAuthors.OrderBy(a => a.StudyNctId).Take(15).ToListAsync();
            foreach (var a in authors)
            {
                Console.WriteLine($"{a.Id,-5} {a.StudyNctId,-16} {a.Pmid,-10} {a.LastName,-25} {a.ForeName,-25} {a.Orcid,-25}");
            }
            Console.WriteLine();

            if (result.Errors != null)
            {
                Console.WriteLine($"Validation errors ({result.Errors.Count}):");
                foreach (var err in result.Errors)
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