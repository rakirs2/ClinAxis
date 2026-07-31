using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class PiFeaturesExportTests
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
        "study_nct_id", "person_id", "full_name", "overall_status", "label",
        "prior_study_count", "prior_completed_count", "prior_enrollment_total", "prior_completion_rate",
        "papers_before_start", "papers_per_year_before_start",
        "medicare_beneficiaries", "medicare_services", "medicare_payments", "medicare_risk_score", "has_medicare",
        "research_payments", "general_payments", "payor_count", "has_payments",
        "current_h_index", "citation_count", "i10_index", "total_papers", "has_metrics",
    ];

    private async Task<string> FetchAsync(string from, string to)
    {
        var response = await _client.GetAsync($"/api/export/training/pi-features?from={from}&to={to}");
        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
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

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Export_WideWindow_ReturnsHeaderAndWindowRows()
    {
        var csv = await FetchAsync("2000-01-01", "2030-12-31");
        var lines = csv.TrimEnd('\n').Split('\n');

        Assert.AreEqual(ExpectedHeader.Length, ParseCsvLine(lines[0]).Length, "header column count");
        CollectionAssert.AreEqual(ExpectedHeader, ParseCsvLine(lines[0]));

        Assert.AreEqual(2, lines.Length, "expected 1 data row (NCT00000009 COMPLETED in window) + header");

        var row = ParseCsvLine(lines[1]);
        Assert.AreEqual("NCT00000009", row[0]);
        Assert.AreEqual(SeedData.Person9.Id.ToString(), row[1]);
        Assert.AreEqual("COMPLETED", row[3]);
        Assert.AreEqual("1", row[4], "COMPLETED label");
        Assert.AreEqual("0", row[5], "no prior history before 2000");
        Assert.AreEqual("0", row[9], "no papers before 2022-09-01 for Person9");
        Assert.AreEqual("0", row[14], "has_medicare");
        Assert.AreEqual("0", row[19], "has_payments");
        Assert.AreEqual("0", row[24], "has_metrics");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Export_ExcludesNonPrincipalRolesAndOtherStatuses()
    {
        var csv = await FetchAsync("2000-01-01", "2030-12-31");
        var lines = csv.TrimEnd('\n').Split('\n');

        Assert.AreEqual(2, lines.Length, "only NCT00000009/PI in window");
        Assert.IsFalse(csv.Contains(SeedData.Person1.Id.ToString(), StringComparison.Ordinal),
            "Person1 (PI of NCT00000002 with null start date) must be excluded");
        Assert.IsFalse(csv.Contains(SeedData.Person2.Id.ToString(), StringComparison.Ordinal),
            "Person2 (SUB_INVESTIGATOR) must be excluded");
        Assert.IsFalse(csv.Contains("NCT00000005", StringComparison.Ordinal),
            "TERMINATED study without PI link must be excluded");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Export_RespectsWindowFiltering()
    {
        var csv = await FetchAsync("2023-01-01", "2023-12-31");
        var lines = csv.TrimEnd('\n').Split('\n');

        Assert.AreEqual(1, lines.Length, "no COMPLETED/TERMINATED study starts in 2023");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Export_DefaultsTo2018To2019Window()
    {
        var csv = await FetchAsync("", "");
        var lines = csv.TrimEnd('\n').Split('\n');

        Assert.AreEqual(1, lines.Length, "golden data has no 2018-2019 studies; default window returns header only");
    }
}
