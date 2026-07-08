using System.Globalization;
using System.Linq;
using Npgsql;
using Scrapers.Models.ClinicalTrialsGov;

namespace Scrapers.Persistence;

public class PostgresStudyRepository
{
    private readonly string _connectionString;

    public PostgresStudyRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string must be provided.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS studies (
                id SERIAL PRIMARY KEY,
                nct_id TEXT UNIQUE NOT NULL,
                brief_title TEXT,
                overall_status TEXT,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS investigators (
                id SERIAL PRIMARY KEY,
                study_id INT NOT NULL REFERENCES studies(id) ON DELETE CASCADE,
                name TEXT NOT NULL,
                affiliation TEXT,
                role TEXT
            );";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> UpsertStudiesAsync(IEnumerable<ClinicalTrialRecord> records, CancellationToken cancellationToken = default)
    {
        var recordList = records.ToList();
        if (recordList.Count == 0)
        {
            return 0;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (var record in recordList)
            {
                var studyId = await UpsertStudyAsync(connection, transaction, record.Summary, cancellationToken).ConfigureAwait(false);
                await ReplaceInvestigatorsAsync(connection, transaction, studyId, record.Investigators, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return recordList.Count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<int> CountStudiesAsync(CancellationToken cancellationToken = default)
        => await ExecuteScalarAsync<int>("SELECT COUNT(*) FROM studies;", cancellationToken).ConfigureAwait(false);

    public async Task<int> CountInvestigatorsAsync(CancellationToken cancellationToken = default)
        => await ExecuteScalarAsync<int>("SELECT COUNT(*) FROM investigators;", cancellationToken).ConfigureAwait(false);

    private static async Task<int> UpsertStudyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, StudySummary summary, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO studies (nct_id, brief_title, overall_status)
            VALUES (@nct_id, @brief_title, @overall_status)
            ON CONFLICT (nct_id) DO UPDATE SET
                brief_title = EXCLUDED.brief_title,
                overall_status = EXCLUDED.overall_status
            RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction)
        {
            Parameters =
            {
                new("@nct_id", summary.NctId ?? (object)DBNull.Value),
                new("@brief_title", summary.BriefTitle ?? (object)DBNull.Value),
                new("@overall_status", summary.OverallStatus ?? (object)DBNull.Value)
            }
        };

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task ReplaceInvestigatorsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int studyId,
        IReadOnlyList<Investigator> investigators,
        CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM investigators WHERE study_id = @study_id;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("@study_id", studyId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (investigators.Count == 0)
        {
            return;
        }

        const string insertSql = @"
            INSERT INTO investigators (study_id, name, affiliation, role)
            VALUES (@study_id, @name, @affiliation, @role);";

        foreach (var investigator in investigators.Where(i => i.HasName))
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction)
            {
                Parameters =
                {
                    new("@study_id", studyId),
                    new("@name", investigator.Name!),
                    new("@affiliation", investigator.Affiliation ?? (object)DBNull.Value),
                    new("@role", investigator.Role ?? (object)DBNull.Value)
                }
            };

            await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<T> ExecuteScalarAsync<T>(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture)!;
    }
}
