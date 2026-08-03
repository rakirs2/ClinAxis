using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class SchemaGuardTests : DbTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task StudiesTable_HasQueryIndexes()
    {
        var indexes = await GetIndexesAsync("studies", "IX_studies_");

        var statusAndDate = indexes.FirstOrDefault(i =>
            i.Contains("overall_status", StringComparison.Ordinal) && i.Contains("start_date", StringComparison.Ordinal));
        Assert.IsNotNull(statusAndDate,
            "Missing index on studies(overall_status, start_date). " +
            $"Found indexes: {string.Join(", ", indexes)}");

        var enrollment = indexes.FirstOrDefault(i => i.Contains("enrollment_count", StringComparison.Ordinal));
        Assert.IsNotNull(enrollment,
            "Missing index on studies(enrollment_count). " +
            $"Found indexes: {string.Join(", ", indexes)}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task InvestigatorPersonsTable_HasQueryIndexes()
    {
        var indexes = await GetIndexesAsync("investigator_persons", "IX_investigator_persons_");

        var match = indexes.FirstOrDefault(i =>
            i.Contains("is_human", StringComparison.Ordinal) && i.Contains("npi_enrichment_result", StringComparison.Ordinal));
        Assert.IsNotNull(match,
            "Missing index on investigator_persons(is_human, npi_enrichment_result). " +
            $"Found indexes: {string.Join(", ", indexes)}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PageViewsTable_HasRequiredColumnsAndIndexes()
    {
        var columns = await GetColumnsAsync("page_views");

        Assert.IsTrue(columns.Any(c => c.Name == "id" && c.Type == "integer"), "id column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "path" && c.Type == "character varying"), "path column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "session_id" && c.Type == "character varying"), "session_id column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "viewed_at" && c.Type == "timestamp with time zone"), "viewed_at column missing or wrong type");

        var indexes = await GetIndexesAsync("page_views", "IX_page_views_");
        Assert.IsNotNull(indexes.FirstOrDefault(i => i.Contains("viewed_at", StringComparison.Ordinal)),
            "Missing index on page_views(viewed_at). " +
            $"Found indexes: {string.Join(", ", indexes)}");
        Assert.IsNotNull(indexes.FirstOrDefault(i => i.Contains("path", StringComparison.Ordinal)),
            "Missing index on page_views(path). " +
            $"Found indexes: {string.Join(", ", indexes)}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PersonIdentifierCandidatesTable_HasEnrichmentFeatureColumns()
    {
        var columns = await GetColumnsAsync("person_identifier_candidates");

        Assert.IsTrue(columns.Any(c => c.Name == "matched_middle_name"), "matched_middle_name column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "matched_credential"), "matched_credential column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "matched_name_prefix"), "matched_name_prefix column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "matched_gender"), "matched_gender column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "matched_city"), "matched_city column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "matched_taxonomy_desc"), "matched_taxonomy_desc column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "matched_taxonomy_state"), "matched_taxonomy_state column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "matched_taxonomy_license"), "matched_taxonomy_license column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "matched_other_names_json" && c.Type == "text"), "matched_other_names_json column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "matched_identifiers_json" && c.Type == "text"), "matched_identifiers_json column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "rule_score" && c.Type == "double precision"), "rule_score column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "model_score" && c.Type == "double precision"), "model_score column missing or wrong type");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RejectedEntitiesTable_HasInvestigatorContextColumns()
    {
        var columns = await GetColumnsAsync("rejected_entities");

        Assert.IsTrue(columns.Any(c => c.Name == "entity_type"), "entity_type column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "value"), "value column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "study_nct_id"), "study_nct_id column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "role" && c.Nullable), "role column missing or not nullable");
        Assert.IsTrue(columns.Any(c => c.Name == "affiliation" && c.Nullable), "affiliation column missing or not nullable");
        Assert.IsTrue(columns.Any(c => c.Name == "rejection_reason" && c.Nullable), "rejection_reason column missing or not nullable");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task StudiesTable_HasRemediatedScalarColumns()
    {
        var columns = await GetColumnsAsync("studies");

        Assert.IsTrue(columns.Any(c => c.Name == "masking"), "masking column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "org_study_id"), "org_study_id column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "lead_sponsor_name"), "lead_sponsor_name column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "collaborator_names"), "collaborator_names column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "eligibility_criteria"), "eligibility_criteria column missing");
        Assert.IsTrue(columns.Any(c => c.Name == "healthy_volunteers"), "healthy_volunteers column missing");
    }

    private async Task<List<string>> GetIndexesAsync(string tableName, string indexPrefix)
    {
        DbContextOptions<ClinicalTrialsContext> opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .ConfigureNpgsql(ConnectionString).Options;
        await using var context = new ClinicalTrialsContext(opts);
        await context.Database.EnsureCreatedAsync();

        string pattern = indexPrefix + "%";
        return await context.Database.SqlQuery<string>($@"
            SELECT indexdef FROM pg_indexes
            WHERE tablename = {tableName}
            AND indexname LIKE {pattern}
        ").ToListAsync();
    }

    private async Task<List<(string Name, string Type, bool Nullable)>> GetColumnsAsync(string tableName)
    {
        DbContextOptions<ClinicalTrialsContext> opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .ConfigureNpgsql(ConnectionString).Options;
        await using var context = new ClinicalTrialsContext(opts);
        await context.Database.EnsureCreatedAsync();

        DbConnection connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_name = @tableName
            ORDER BY ordinal_position";
        command.Parameters.Add(new Npgsql.NpgsqlParameter("@tableName", tableName));

        await using DbDataReader reader = await command.ExecuteReaderAsync();
        var columns = new List<(string Name, string Type, bool Nullable)>();
        while (await reader.ReadAsync())
        {
            columns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2) == "YES"));
        }

        return columns;
    }
}
