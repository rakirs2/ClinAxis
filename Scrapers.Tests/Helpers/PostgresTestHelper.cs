using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace Scrapers.Tests.Helpers;

internal static class PostgresTestHelper
{
    internal static string ConnectionString => Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
        ?? throw new AssertFailedException("POSTGRES_CONNECTION_STRING environment variable must be set for database integration tests.");

    internal static async Task ClearDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        const string sql = "TRUNCATE TABLE investigators RESTART IDENTITY CASCADE; TRUNCATE TABLE studies RESTART IDENTITY CASCADE;";
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
