using Npgsql;

namespace Scrapers;

public static class ConnectionStringProvider
{
    public static string Default =>
        Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
        ?? $"Host=localhost;Port=5432;Database=clinical_trial_data;Username={Environment.UserName}";

    public static string DefaultNoPooling =>
        $"{Default};Pooling=false";

    public static string WithPoolLimits(string connectionString, int maxPoolSize = 25)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            MaxPoolSize = maxPoolSize,
            ConnectionIdleLifetime = 300,
            ConnectionPruningInterval = 60
        };
        return builder.ConnectionString;
    }
}
