using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using NpgsqlTypes;
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

        DbContextOptions<ClinicalTrialsContext> opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .UseNpgsql(ConnectionString).Options;
        using var ctx = new ClinicalTrialsContext(opts);
        await ctx.Database.MigrateAsync().ConfigureAwait(false);

        // Verify migration created the studies table; some CI environments
        // exhibit a race where MigrateAsync succeeds but tables are absent.
        if (!await TableExistsAsync(ConnectionString, "studies").ConfigureAwait(false))
        {
            await ctx.Database.EnsureDeletedAsync().ConfigureAwait(false);
            await ctx.Database.MigrateAsync().ConfigureAwait(false);
        }

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

    private static async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = @p)";
        cmd.Parameters.AddWithValue("p", NpgsqlTypes.NpgsqlDbType.Text, tableName);
        var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        return result is bool b && b;
    }
}
