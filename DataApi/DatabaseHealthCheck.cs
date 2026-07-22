using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scrapers;
using Scrapers.Persistence;

namespace DataApi;

internal sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public DatabaseHealthCheck(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var ctx = new ClinicalTrialsContext(
                new DbContextOptionsBuilder<ClinicalTrialsContext>()
                    .ConfigureNpgsql(_connectionString)
                    .Options);

            var canConnect = await ctx.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("Database is reachable")
                : HealthCheckResult.Unhealthy("Cannot connect to database");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Health check cancelled");
        }
        catch (Npgsql.NpgsqlException ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}
