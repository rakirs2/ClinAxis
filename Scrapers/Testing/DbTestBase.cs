using Microsoft.EntityFrameworkCore;
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
            .WithCleanUp(false)
            .Build();
        await container.StartAsync().ConfigureAwait(false);
        _container = container;
        _adminConnectionString = container.GetConnectionString();
    }

    [TestInitialize]
    public async Task Init()
    {
        await Initialize.Value.ConfigureAwait(false);

        // Each test method gets its own database for isolation
        // (StudyRepository creates its own connections, so txn rollback won't cover it)
        var dbName = "ct_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var adminBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = "postgres"
        };
        using var adminConn = new NpgsqlConnection(adminBuilder.ConnectionString);
        await adminConn.OpenAsync().ConfigureAwait(false);
        using NpgsqlCommand createCmd = adminConn.CreateCommand();
        createCmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
        await createCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = dbName
        }.ConnectionString;

        // Verify the new database is reachable and migrate
        await using (var verifyConn = new NpgsqlConnection(ConnectionString))
        {
            await verifyConn.OpenAsync().ConfigureAwait(false);
            Assert.AreEqual(dbName, verifyConn.Database, "Connection should target the new database");
        }

        DbContextOptions<ClinicalTrialsContext> opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .UseNpgsql(ConnectionString).Options;
        using var ctx = new ClinicalTrialsContext(opts);
        await ctx.Database.MigrateAsync().ConfigureAwait(false);

        Context = new ClinicalTrialsContext(opts);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (Context != null)
        {
            await Context.DisposeAsync().ConfigureAwait(false);
        }
    }
}
