using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;

namespace Scrapers.IntegrationTests.Helpers;

internal static class PostgresTestHelper
{
    internal static string ConnectionString =>
        Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
        ?? LoadFromFile()
        ?? ConnectionStringProvider.Default;

    private static string? LoadFromFile()
    {
        var envPath = Path.Combine(AppContext.BaseDirectory, ".integrationtests.env");
        if (!File.Exists(envPath))
        {
            return null;
        }

        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("POSTGRES_CONNECTION_STRING=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[("POSTGRES_CONNECTION_STRING=".Length)..];
            }
        }

        return null;
    }

    internal static DbContextOptions<ClinicalTrialsContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<ClinicalTrialsContext>().UseNpgsql(ConnectionString).Options;
    }

    internal static async Task ClearDatabaseAsync()
    {
        await using var context = new ClinicalTrialsContext(CreateOptions());
        await context.Database.MigrateAsync();
        context.Investigators.RemoveRange(context.Investigators);
        context.Studies.RemoveRange(context.Studies);
        await context.SaveChangesAsync();
    }
}
