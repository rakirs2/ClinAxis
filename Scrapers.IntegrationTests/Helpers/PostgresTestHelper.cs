using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;

namespace Scrapers.IntegrationTests.Helpers;

internal static class PostgresTestHelper
{
    internal static string ConnectionString => Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
        ?? throw new AssertFailedException("POSTGRES_CONNECTION_STRING environment variable must be set for integration tests.");

    internal static DbContextOptions<ClinicalTrialsContext> CreateOptions()
        => new DbContextOptionsBuilder<ClinicalTrialsContext>().UseNpgsql(ConnectionString).Options;

    internal static async Task ClearDatabaseAsync()
    {
        await using var context = new ClinicalTrialsContext(CreateOptions());
        await context.Database.MigrateAsync();
        context.Investigators.RemoveRange(context.Investigators);
        context.Studies.RemoveRange(context.Studies);
        await context.SaveChangesAsync();
    }
}
