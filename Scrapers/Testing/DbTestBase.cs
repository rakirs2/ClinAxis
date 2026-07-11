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
            .Build();
        await container.StartAsync();
        _container = container;
        _adminConnectionString = container.GetConnectionString();
    }

    [TestInitialize]
    public async Task Init()
    {
        await Initialize.Value;

        // Each test method gets its own database for isolation
        // (StudyRepository creates its own connections, so txn rollback won't cover it)
        var dbName = "ct_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var adminBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = "postgres"
        };
        await using var adminConn = new NpgsqlConnection(adminBuilder.ConnectionString);
        await adminConn.OpenAsync();
        await using NpgsqlCommand createCmd = adminConn.CreateCommand();
        createCmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
        await createCmd.ExecuteNonQueryAsync();

        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = dbName
        }.ConnectionString;

        // Run migrations on this fresh database
        DbContextOptions<ClinicalTrialsContext> opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .UseNpgsql(ConnectionString).Options;
        await using var ctx = new ClinicalTrialsContext(opts);
        await ctx.Database.MigrateAsync();

        Context = new ClinicalTrialsContext(opts);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (Context != null)
        {
            await Context.DisposeAsync();
        }
    }
}
