using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Scrapers;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

var cs = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? $"Host=localhost;Port=5432;Database=clinical_trial_data;Username={Environment.UserName}";

var opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
    .ConfigureNpgsql(cs)
    .Options;

using var resetCtx = new ClinicalTrialsContext(opts);
await resetCtx.Database.EnsureDeletedAsync().ConfigureAwait(false);
await resetCtx.Database.MigrateAsync().ConfigureAwait(false);

var meshJsonPath = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "Scrapers", "Resources", "mesh", "mesh_terms.json"));
if (!File.Exists(meshJsonPath))
{
    meshJsonPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Scrapers", "Resources", "mesh", "mesh_terms.json"));
}

var meshJson = await File.ReadAllTextAsync(meshJsonPath).ConfigureAwait(false);
using var doc = JsonDocument.Parse(meshJson);
var names = doc.RootElement.GetProperty("names").EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
var cuis = doc.RootElement.GetProperty("cuis").EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
var treeNums = doc.RootElement.GetProperty("tree_numbers").EnumerateArray().Select(arr =>
    arr.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray()).ToArray();
var cats = doc.RootElement.GetProperty("categories").EnumerateArray().Select(e => e.GetString() ?? "").ToArray();

var seenCuis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var descriptors = new List<MeshDescriptorEntity>(names.Length);
for (int i = 0; i < names.Length; i++)
{
    var cui = i < cuis.Length ? cuis[i] : "";
    if (string.IsNullOrEmpty(cui) || !seenCuis.Add(cui))
        continue;
    descriptors.Add(new MeshDescriptorEntity
    {
        Cui = cui,
        Name = names[i],
        TreeNumbers = i < treeNums.Length ? treeNums[i] : [],
        Category = i < cats.Length ? cats[i] : "",
    });
}

resetCtx.MeshDescriptors.AddRange(descriptors);
await resetCtx.SaveChangesAsync().ConfigureAwait(false);

var allDesc = await resetCtx.MeshDescriptors
    .Where(d => d.TreeNumbers != null && d.TreeNumbers.Count > 0)
    .Select(d => new { d.Id, d.TreeNumbers })
    .ToListAsync()
    .ConfigureAwait(false);

var treePaths = new List<MeshTreePathEntity>();
foreach (var d in allDesc)
{
    treePaths.AddRange(d.TreeNumbers.Select(tn => new MeshTreePathEntity
    {
        MeshDescriptorId = d.Id,
        TreeNumber = tn
    }));
}
resetCtx.Set<MeshTreePathEntity>().AddRange(treePaths);
await resetCtx.SaveChangesAsync().ConfigureAwait(false);

var targetCuis = new Dictionary<string, string>
{
    ["D009765"] = "Obesity",
    ["D002318"] = "Major Adverse Cardiac Events",
    ["D006333"] = "Myocardial Failure",
    ["D001249"] = "Asthma",
    ["D008545"] = "Melanoma",
    ["D003924"] = "Diabetes Mellitus, Type 2",
    ["D003865"] = "Paraphrenia, Involutional",
    ["D000544"] = "Familial Alzheimer Disease (FAD)",
    ["D003072"] = "Overinclusion",
    ["D006973"] = "Hypertension",
    ["D030342"] = "Single-Gene Defects",
    ["D015415"] = "Biochemical Marker",
};

var cuidToId = await resetCtx.MeshDescriptors
    .Where(d => targetCuis.Keys.Contains(d.Cui))
    .ToDictionaryAsync(d => d.Cui, d => d.Id)
    .ConfigureAwait(false);

var person = new Func<Guid, string, InvestigatorPersonEntity>((id, name) => new InvestigatorPersonEntity
{
    Id = id, FullName = name, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
});

var p1 = person(Guid.Parse("A0000000-0000-0000-0000-000000000001"), "Dr. Alice Johnson, MD, PhD");
var p2 = person(Guid.Parse("A0000000-0000-0000-0000-000000000002"), "Prof. Bob Williams, MD");
var p3 = person(Guid.Parse("A0000000-0000-0000-0000-000000000003"), "Prof. Carol Davis, PhD");
var p4 = person(Guid.Parse("A0000000-0000-0000-0000-000000000004"), "Dr. James Smith, MD");
var p5 = person(Guid.Parse("A0000000-0000-0000-0000-000000000005"), "Prof. Dr. Sarah Campbell, MSc");
var p6 = person(Guid.Parse("A0000000-0000-0000-0000-000000000006"), "Dr. Michael Chen, PhD");
var p7 = person(Guid.Parse("A0000000-0000-0000-0000-000000000007"), "Prof. Robert Miller, MD, MSc");
var p8 = person(Guid.Parse("A0000000-0000-0000-0000-000000000008"), "Dr. Anna Martinez, PhD");
var p9 = person(Guid.Parse("A0000000-0000-0000-0000-000000000009"), "Dr. David Williams, MD");
var p10 = person(Guid.Parse("A0000000-0000-0000-0000-000000000010"), "Prof. Emily Brown, MD, PhD");
var p11 = person(Guid.Parse("A0000000-0000-0000-0000-000000000011"), "Dr. Christopher Lee, MSc");
var p12 = person(Guid.Parse("A0000000-0000-0000-0000-000000000012"), "Prof. Jennifer Garcia, MD, MSc");

resetCtx.InvestigatorPersons.AddRange(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12);
await resetCtx.SaveChangesAsync().ConfigureAwait(false);

int nid(string cui) => cuidToId.GetValueOrDefault(cui, 0);
if (cuidToId.Count < 12) throw new InvalidOperationException("Not all target CUIs found in DB");

var s2 = new StudyEntity { NctId = "NCT00000002", BriefTitle = "Pregabalin for Neuropathic Pain Relief Trial", OverallStatus = "COMPLETED", EnrollmentCount = 200,
    Conditions = [new StudyConditionEntity { MeshDescriptorId = nid("D009765") }],
    Phases = [new StudyPhaseEntity { Phase = "PHASE3" }] };
var s3 = new StudyEntity { NctId = "NCT00000003", BriefTitle = "Aspirin Cardiovascular Prevention Study", OverallStatus = "RECRUITING", EnrollmentCount = 5000,
    Conditions = [new StudyConditionEntity { MeshDescriptorId = nid("D002318") }, new StudyConditionEntity { MeshDescriptorId = nid("D006333") }],
    Phases = [new StudyPhaseEntity { Phase = "PHASE2" }] };
var s4 = new StudyEntity { NctId = "NCT00000004", BriefTitle = "Pediatric Asthma Treatment Study", OverallStatus = "RECRUITING", EnrollmentCount = 150,
    Conditions = [new StudyConditionEntity { MeshDescriptorId = nid("D001249") }],
    Phases = [new StudyPhaseEntity { Phase = "PHASE4" }] };
var s5 = new StudyEntity { NctId = "NCT00000005", BriefTitle = "Cancer Immunotherapy Trial", OverallStatus = "TERMINATED", EnrollmentCount = 50,
    Conditions = [new StudyConditionEntity { MeshDescriptorId = nid("D008545") }],
    Phases = [new StudyPhaseEntity { Phase = "PHASE1" }] };
var s6 = new StudyEntity { NctId = "NCT00000006", BriefTitle = "Diabetes Type 2 Management Study", OverallStatus = "RECRUITING", EnrollmentCount = 300,
    StartDate = new DateOnly(2024, 1, 15),
    Conditions = [new StudyConditionEntity { MeshDescriptorId = nid("D003924") }],
    Phases = [new StudyPhaseEntity { Phase = "PHASE3" }],
    Locations = [new StudyLocationEntity { Country = "United States", State = "CA", City = "San Francisco", Facility = "UCSF Medical Center" }, new StudyLocationEntity { Country = "United States", State = "NY", City = "New York", Facility = "Columbia Presbyterian" }] };
var s7 = new StudyEntity { NctId = "NCT00000007", BriefTitle = "Depression Treatment Trial - Germany", OverallStatus = "ACTIVE", EnrollmentCount = 120,
    StartDate = new DateOnly(2023, 6, 1),
    Conditions = [new StudyConditionEntity { MeshDescriptorId = nid("D003865") }],
    Phases = [new StudyPhaseEntity { Phase = "PHASE2" }],
    Locations = [new StudyLocationEntity { Country = "Germany", State = "Berlin", City = "Berlin", Facility = "Charité Hospital" }] };
var s8 = new StudyEntity { NctId = "NCT00000008", BriefTitle = "Alzheimer's Disease Biomarker Study", OverallStatus = "RECRUITING", EnrollmentCount = 500,
    StartDate = new DateOnly(2024, 3, 10),
    Conditions = [new StudyConditionEntity { MeshDescriptorId = nid("D000544") }, new StudyConditionEntity { MeshDescriptorId = nid("D003072") }],
    Phases = [new StudyPhaseEntity { Phase = "PHASE3" }],
    Locations = [new StudyLocationEntity { Country = "United States", State = "MA", City = "Boston", Facility = "Massachusetts General Hospital" }, new StudyLocationEntity { Country = "United States", State = "CA", City = "Los Angeles", Facility = "UCLA" }, new StudyLocationEntity { Country = "United States", State = "IL", City = "Chicago", Facility = "Northwestern University" }] };
var s9 = new StudyEntity { NctId = "NCT00000009", BriefTitle = "Hypertension Management in Canada", OverallStatus = "COMPLETED", EnrollmentCount = 250,
    StartDate = new DateOnly(2022, 9, 1),
    Conditions = [new StudyConditionEntity { MeshDescriptorId = nid("D006973") }],
    Phases = [new StudyPhaseEntity { Phase = "PHASE4" }],
    Locations = [new StudyLocationEntity { Country = "Canada", State = "Ontario", City = "Toronto", Facility = "Toronto General Hospital" }, new StudyLocationEntity { Country = "Canada", State = "British Columbia", City = "Vancouver", Facility = "Vancouver Hospital" }] };
var s10 = new StudyEntity { NctId = "NCT00000010", BriefTitle = "Small Enrollment Low Phase Study", OverallStatus = "NOT_YET_RECRUITING", EnrollmentCount = 20,
    StartDate = new DateOnly(2025, 1, 1),
    Conditions = [new StudyConditionEntity { MeshDescriptorId = nid("D030342") }],
    Phases = [new StudyPhaseEntity { Phase = "PHASE1" }],
    Locations = [new StudyLocationEntity { Country = "United States", State = "TX", City = "Houston", Facility = "Texas Medical Center" }] };
var s11 = new StudyEntity { NctId = "NCT00000011", BriefTitle = "Large Enrollment Phase 3 Multi-Site Study", OverallStatus = "RECRUITING", EnrollmentCount = 5000,
    StartDate = new DateOnly(2023, 1, 15),
    Conditions = [new StudyConditionEntity { MeshDescriptorId = nid("D006973") }, new StudyConditionEntity { MeshDescriptorId = nid("D003924") }],
    Phases = [new StudyPhaseEntity { Phase = "PHASE3" }],
    Locations = [new StudyLocationEntity { Country = "United States", State = "NY", City = "New York", Facility = "Cornell Weill Medical" }, new StudyLocationEntity { Country = "United States", State = "CA", City = "San Diego", Facility = "UCSD Medical Center" }] };
var s12 = new StudyEntity { NctId = "NCT00000012", BriefTitle = "Observational Biomarker Discovery Study", OverallStatus = "RECRUITING", EnrollmentCount = 500,
    StartDate = new DateOnly(2024, 6, 1),
    Conditions = [new StudyConditionEntity { MeshDescriptorId = nid("D015415") }],
    Phases = [new StudyPhaseEntity { Phase = "NA" }],
    Locations = [new StudyLocationEntity { Country = "United States", State = "CA", City = "Stanford", Facility = "Stanford Medical Center" }] };
var s1 = new StudyEntity { NctId = "NCT00000001", BriefTitle = "Test Study Alpha", OverallStatus = "ACTIVE" };

resetCtx.Studies.AddRange(s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12);
await resetCtx.SaveChangesAsync().ConfigureAwait(false);

resetCtx.StudyInvestigators.AddRange(
    new StudyInvestigatorEntity { StudyNctId = "NCT00000002", InvestigatorPersonId = p1.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true },
    new StudyInvestigatorEntity { StudyNctId = "NCT00000002", InvestigatorPersonId = p2.Id, RoleOnStudy = "SUB_INVESTIGATOR", IsOverallOfficial = true },
    new StudyInvestigatorEntity { StudyNctId = "NCT00000003", InvestigatorPersonId = p1.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true },
    new StudyInvestigatorEntity { StudyNctId = "NCT00000004", InvestigatorPersonId = p3.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true },
    new StudyInvestigatorEntity { StudyNctId = "NCT00000006", InvestigatorPersonId = p4.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true },
    new StudyInvestigatorEntity { StudyNctId = "NCT00000006", InvestigatorPersonId = p5.Id, RoleOnStudy = "SUB_INVESTIGATOR", IsOverallOfficial = true },
    new StudyInvestigatorEntity { StudyNctId = "NCT00000007", InvestigatorPersonId = p6.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true },
    new StudyInvestigatorEntity { StudyNctId = "NCT00000008", InvestigatorPersonId = p7.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true },
    new StudyInvestigatorEntity { StudyNctId = "NCT00000008", InvestigatorPersonId = p8.Id, RoleOnStudy = "SUB_INVESTIGATOR", IsOverallOfficial = true },
    new StudyInvestigatorEntity { StudyNctId = "NCT00000009", InvestigatorPersonId = p9.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true },
    new StudyInvestigatorEntity { StudyNctId = "NCT00000010", InvestigatorPersonId = p10.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true },
    new StudyInvestigatorEntity { StudyNctId = "NCT00000011", InvestigatorPersonId = p11.Id, RoleOnStudy = "PRINCIPAL_INVESTIGATOR", IsOverallOfficial = true },
    new StudyInvestigatorEntity { StudyNctId = "NCT00000011", InvestigatorPersonId = p12.Id, RoleOnStudy = "SUB_INVESTIGATOR", IsOverallOfficial = true }
);
await resetCtx.SaveChangesAsync().ConfigureAwait(false);

var paper1 = new PubmedPaperEntity
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
var paper2 = new PubmedPaperEntity
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
resetCtx.PubmedPapers.AddRange(paper1, paper2);
await resetCtx.SaveChangesAsync().ConfigureAwait(false);

resetCtx.StudyPapers.AddRange(
    new StudyPaperEntity { StudyNctId = s2.NctId, PubmedPaperId = paper1.Id },
    new StudyPaperEntity { StudyNctId = s2.NctId, PubmedPaperId = paper2.Id },
    new StudyPaperEntity { StudyNctId = s3.NctId, PubmedPaperId = paper1.Id }
);
await resetCtx.SaveChangesAsync().ConfigureAwait(false);

resetCtx.InvestigatorPapers.AddRange(
    new InvestigatorPaperEntity { InvestigatorPersonId = p1.Id, PubmedPaperId = paper1.Id, AuthorPosition = 1, IsCorrespondingAuthor = true },
    new InvestigatorPaperEntity { InvestigatorPersonId = p1.Id, PubmedPaperId = paper2.Id, AuthorPosition = 2, IsCorrespondingAuthor = false },
    new InvestigatorPaperEntity { InvestigatorPersonId = p3.Id, PubmedPaperId = paper1.Id, AuthorPosition = 3, IsCorrespondingAuthor = false }
);
await resetCtx.SaveChangesAsync().ConfigureAwait(false);
