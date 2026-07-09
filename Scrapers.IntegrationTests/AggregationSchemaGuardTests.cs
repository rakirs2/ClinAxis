using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.IntegrationTests.Helpers;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class AggregationSchemaGuardTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task PiAggregationsTable_HasRequiredColumns()
    {
        DbContextOptions<ClinicalTrialsContext> options = PostgresTestHelper.CreateOptions();
        await using var context = new ClinicalTrialsContext(options);
        await context.Database.MigrateAsync();

        DbConnection connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_name = 'pi_aggregations'
            ORDER BY ordinal_position";

        await using DbDataReader reader = await command.ExecuteReaderAsync();
        var columns = new List<(string Name, string Type, bool Nullable)>();
        while (await reader.ReadAsync())
        {
            columns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2) == "YES"));
        }

        Assert.IsTrue(columns.Any(c => c.Name == "id" && c.Type == "integer"), "id column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "investigator_name" && c.Type == "character varying"), "investigator_name column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "study_count" && c.Type == "integer"), "study_count column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "pubmed_paper_count" && c.Type == "integer"), "pubmed_paper_count column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "study_nct_ids" && c.Type == "text"), "study_nct_ids column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "computed_at" && c.Type == "timestamp with time zone"), "computed_at column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "affiliation" && c.Type == "text" && c.Nullable), "affiliation column missing, wrong type, or not nullable");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CategoryAggregationsTable_HasRequiredColumns()
    {
        DbContextOptions<ClinicalTrialsContext> options = PostgresTestHelper.CreateOptions();
        await using var context = new ClinicalTrialsContext(options);
        await context.Database.MigrateAsync();

        DbConnection connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_name = 'category_aggregations'
            ORDER BY ordinal_position";

        await using DbDataReader reader = await command.ExecuteReaderAsync();
        var columns = new List<(string Name, string Type, bool Nullable)>();
        while (await reader.ReadAsync())
        {
            columns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2) == "YES"));
        }

        Assert.IsTrue(columns.Any(c => c.Name == "id" && c.Type == "integer"), "id column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "category_name" && c.Type == "character varying"), "category_name column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "category_type" && c.Type == "character varying"), "category_type column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "study_count" && c.Type == "integer"), "study_count column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "pubmed_paper_count" && c.Type == "integer"), "pubmed_paper_count column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "study_nct_ids" && c.Type == "text"), "study_nct_ids column missing or wrong type");
        Assert.IsTrue(columns.Any(c => c.Name == "computed_at" && c.Type == "timestamp with time zone"), "computed_at column missing or wrong type");
    }
}