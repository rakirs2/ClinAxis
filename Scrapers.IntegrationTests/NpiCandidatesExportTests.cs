using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class NpiCandidatesExportTests
{
    private SnapshotDb _snapshot = null!;
    private WebApplicationFactory<DataApi.Program> _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Init()
    {
        _snapshot = new SnapshotDb();
        Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", _snapshot.ConnectionString);
        _factory = new WebApplicationFactory<DataApi.Program>();
        _client = _factory.CreateClient();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _snapshot.DisposeAsync();
    }

    private static string[] ExpectedHeader =>
    [
        "person_id", "full_name", "batch_size",
        "exact_name_match",
        "middle_name_match", "has_middle_name_match",
        "credential_match", "has_credential_match",
        "state_match", "has_state_match",
        "city_match", "has_city_match",
        "org_match", "has_org_match",
        "other_name_match", "has_other_name_match",
        "specialty_match", "has_specialty_match",
        "license_state_match", "has_license_state_match",
        "department_match", "has_department_match",
        "orcid_match", "deactivated",
        "rule_score", "label",
    ];

    private static string Today => DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Export_ResolvedBatchesOnly_ReturnsLabeledCandidates()
    {
        var resolvedPerson = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. John A Smith, MD",
            IsHuman = true,
            Orcid = "0000-0001-2345-6789",
            Npi = "1234567890",
            NpiEnrichmentResult = "assigned",
            NpiLookupAttemptedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        };
        var ambiguousPerson = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Ambiguous Person",
            IsHuman = true,
            NpiEnrichmentResult = "ambiguous",
            NpiLookupAttemptedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        };
        var lostBatchPerson = new InvestigatorPersonEntity
        {
            Id = Guid.NewGuid(),
            FullName = "Dr. Lost Approval",
            IsHuman = true,
            NpiEnrichmentResult = "assigned",
            NpiLookupAttemptedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        };

        using (var ctx = new ClinicalTrialsContext(new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .ConfigureNpgsql(_snapshot.ConnectionString).Options))
        {
            ctx.InvestigatorPersons.AddRange(resolvedPerson, ambiguousPerson, lostBatchPerson);
            ctx.InvestigatorAffiliations.Add(new InvestigatorAffiliationEntity
            {
                InvestigatorPersonId = resolvedPerson.Id,
                InstitutionName = "Mayo Clinic",
                Department = "Cardiology",
                City = "Rochester",
                State = "MN",
                IsPrimary = true
            });
            ctx.PersonIdentifierCandidates.AddRange(
                new PersonIdentifierCandidateEntity
                {
                    PersonId = resolvedPerson.Id,
                    IdentifierType = "NPI",
                    IdentifierValue = "1234567890",
                    SourceName = "NPPES",
                    MatchedFullName = "John Smith",
                    MatchedMiddleName = "A",
                    MatchedCredential = "MD",
                    MatchedState = "MN",
                    MatchedCity = "Rochester",
                    MatchedAffiliation = "Mayo Clinic",
                    MatchedTaxonomyDesc = "Cardiovascular Disease",
                    MatchedTaxonomyState = "MN",
                    MatchedTaxonomyLicense = "12345",
                    SourceStatus = "A",
                    RuleScore = 0.85,
                    IsAutoApproved = true,
                    CreatedAt = DateTime.UtcNow
                },
                new PersonIdentifierCandidateEntity
                {
                    PersonId = resolvedPerson.Id,
                    IdentifierType = "NPI",
                    IdentifierValue = "9999999999",
                    SourceName = "NPPES",
                    MatchedFullName = "James Smith",
                    MatchedState = "CA",
                    SourceStatus = "A",
                    RuleScore = 0.35,
                    IsAutoApproved = false,
                    CreatedAt = DateTime.UtcNow
                },
                new PersonIdentifierCandidateEntity
                {
                    PersonId = ambiguousPerson.Id,
                    IdentifierType = "NPI",
                    IdentifierValue = "5555555555",
                    SourceName = "NPPES",
                    MatchedFullName = "Ambi Smith",
                    SourceStatus = "A",
                    RuleScore = 0.5,
                    IsAutoApproved = false,
                    CreatedAt = DateTime.UtcNow
                },
                new PersonIdentifierCandidateEntity
                {
                    PersonId = lostBatchPerson.Id,
                    IdentifierType = "NPI",
                    IdentifierValue = "7777777777",
                    SourceName = "NPPES",
                    MatchedFullName = "Lost Smith",
                    SourceStatus = "A",
                    RuleScore = 0.6,
                    IsAutoApproved = false,
                    CreatedAt = DateTime.UtcNow
                });
            await ctx.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/export/training/npi-candidates?from=2000-01-01&to={Today}");
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        var lines = csv.TrimEnd('\n').Split('\n');

        CollectionAssert.AreEqual(ExpectedHeader, ParseCsvLine(lines[0]));
        Assert.AreEqual(3, lines.Length, "expected 1 header + 2 resolved-batch rows");

        var approved = ParseCsvLine(lines[1]);
        Assert.AreEqual(resolvedPerson.Id.ToString(), approved[0]);
        Assert.AreEqual("2", approved[2], "batch_size");
        Assert.AreEqual("1", approved[3], "exact_name_match");
        Assert.AreEqual("1", approved[4], "middle_name_match");
        Assert.AreEqual("1", approved[5], "has_middle_name_match");
        Assert.AreEqual("1", approved[6], "credential_match");
        Assert.AreEqual("1", approved[8], "state_match");
        Assert.AreEqual("1", approved[10], "city_match");
        Assert.AreEqual("1", approved[12], "org_match");
        Assert.AreEqual("1", approved[18], "license_state_match");
        Assert.AreEqual("1", approved[20], "department_match");
        Assert.AreEqual("0", approved[22], "orcid_match (no stored identifiers)");
        Assert.AreEqual("0", approved[23], "deactivated");
        Assert.AreEqual("0.85", approved[24], "rule_score");
        Assert.AreEqual("1", approved[25], "label");

        var rejected = ParseCsvLine(lines[2]);
        Assert.AreEqual(resolvedPerson.Id.ToString(), rejected[0]);
        Assert.AreEqual("0", rejected[3], "exact_name_match");
        Assert.AreEqual("0", rejected[8], "state_match (MN vs CA)");
        Assert.AreEqual("0.35", rejected[24], "rule_score");
        Assert.AreEqual("0", rejected[25], "label");

        Assert.IsFalse(csv.Contains(ambiguousPerson.Id.ToString(), StringComparison.Ordinal), "ambiguous batch excluded");
        Assert.IsFalse(csv.Contains(lostBatchPerson.Id.ToString(), StringComparison.Ordinal), "batch without approved candidate excluded");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Export_MissingWindowParams_Returns400()
    {
        var response = await _client.GetAsync("/api/export/training/npi-candidates");

        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
