using Microsoft.EntityFrameworkCore;
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
        DockerRetry.StartWithRetry(_container);
        ConnectionString = _container.GetConnectionString();

        DbContextOptions<ClinicalTrialsContext> opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .UseNpgsql(ConnectionString).Options;
        var ctx = new ClinicalTrialsContext(opts);
        // Create schema based on EF Core model (no migrations needed until production)
        ctx.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
        SeedData.SeedAsync(ctx).GetAwaiter().GetResult();
        ctx.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_persist)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }
}
