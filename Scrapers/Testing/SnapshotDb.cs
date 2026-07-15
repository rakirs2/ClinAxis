using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Scrapers.Persistence;
using Testcontainers.PostgreSql;

namespace Scrapers.Testing;

public sealed class SnapshotDb : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container;
    private readonly bool _persist;
    public string ConnectionString { get; }

    public SnapshotDb(bool persist = false)
    {
        _persist = persist;
        var dbName = persist
            ? "clinical_trial_data_snapshot"
            : "ct_snapshot_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        _container = new PostgreSqlBuilder()
            .WithDatabase(dbName)
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        _container.StartAsync().GetAwaiter().GetResult();
        ConnectionString = _container.GetConnectionString();

        DbContextOptions<ClinicalTrialsContext> opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .UseNpgsql(ConnectionString).Options;
        var ctx = new ClinicalTrialsContext(opts);
        ctx.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
        SeedMigrationHistory(ctx);
        SeedData.SeedAsync(ctx).GetAwaiter().GetResult();
        ctx.Dispose();
    }

    private static void SeedMigrationHistory(ClinicalTrialsContext ctx)
    {
        var services = ctx.GetInfrastructure();
        var migrationsAssembly = services.GetRequiredService<IMigrationsAssembly>();
        using var conn = new NpgsqlConnection(ctx.Database.GetConnectionString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (""MigrationId"" text NOT NULL, ""ProductVersion"" text NOT NULL, PRIMARY KEY (""MigrationId""));";
        cmd.ExecuteNonQuery();
        foreach (var migrationId in migrationsAssembly.Migrations.Keys)
        {
            cmd.CommandText = $@"INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"") SELECT '{migrationId}', '9.0.0' WHERE NOT EXISTS (SELECT 1 FROM ""__EFMigrationsHistory"" WHERE ""MigrationId"" = '{migrationId}')";
            cmd.ExecuteNonQuery();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_persist)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }
}
