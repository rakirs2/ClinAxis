using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Scrapers.Persistence;
using Testcontainers.PostgreSql;

namespace Scrapers.Testing;

[TestClass]
public abstract class DbTestBase
{
    private static PostgreSqlContainer? _container;
    private static readonly Lazy<Task> Initialize = new(InitializeAsync);
    private static string _adminConnectionString = "";
    protected string ConnectionString { get; private set; } = "";
    protected ClinicalTrialsContext Context { get; private set; } = null!;
    private static async Task InitializeAsync()
    {
        PostgreSqlContainer container = new PostgreSqlBuilder()
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await container.StartAsync().ConfigureAwait(false);
        _container = container;
        _adminConnectionString = container.GetConnectionString();
    }
    [TestInitialize]
    public async Task Init()
    {
        await Initialize.Value.ConfigureAwait(false);
        var dbName = "ct_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var adminBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = "postgres" };
        using var adminConn = new NpgsqlConnection(adminBuilder.ConnectionString);
        await adminConn.OpenAsync().ConfigureAwait(false);
        using NpgsqlCommand createCmd = adminConn.CreateCommand();
        createCmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
        await createCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = dbName }.ConnectionString;
        DbContextOptions<ClinicalTrialsContext> opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .UseNpgsql(ConnectionString).Options;
        using var ctx = new ClinicalTrialsContext(opts);
        await ctx.Database.EnsureCreatedAsync().ConfigureAwait(false);
        await SeedMigrationHistoryAsync(ctx).ConfigureAwait(false);
        Context = new ClinicalTrialsContext(opts);
    }
    [TestCleanup]
    public async Task Cleanup()
    {
        if (Context != null)
            await Context.DisposeAsync().ConfigureAwait(false);
    }
    private static async Task SeedMigrationHistoryAsync(ClinicalTrialsContext ctx)
    {
        var services = ctx.GetInfrastructure();
        var migrationsAssembly = services.GetRequiredService<IMigrationsAssembly>();
        var connection = services.GetRequiredService<Microsoft.EntityFrameworkCore.Storage.IRelationalConnection>();
        await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            await using var cmd = connection.DbConnection.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (""MigrationId"" text NOT NULL, ""ProductVersion"" text NOT NULL, PRIMARY KEY (""MigrationId""));";
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            foreach (var migrationId in migrationsAssembly.Migrations.Keys)
            {
                cmd.CommandText = $@"INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"") SELECT '{migrationId}', '9.0.0' WHERE NOT EXISTS (SELECT 1 FROM ""__EFMigrationsHistory"" WHERE ""MigrationId"" = '{migrationId}')";
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await connection.CloseAsync().ConfigureAwait(false);
        }
    }
}
