using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Scrapers.Tests.Helpers;

internal static class PostgresTestHelper
{
    internal static string ConnectionString => Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
        ?? throw new AssertFailedException("POSTGRES_CONNECTION_STRING environment variable must be set for database integration tests.");

    internal static async Task ClearDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<ClinicalTrialsContext>().UseNpgsql(ConnectionString).Options;
        await using var context = new ClinicalTrialsContext(options);
        await context.Database.MigrateAsync();
        context.Investigators.RemoveRange(context.Investigators);
        context.Studies.RemoveRange(context.Studies);
        await context.SaveChangesAsync();
    }
}
