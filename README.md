# ClinicalTrialData
Fetch and persist structured snapshots from [ClinicalTrials.gov](https://clinicaltrials.gov) so investigators and studies can be analyzed locally.

## Requirements
- .NET 10 SDK (ships with this repo via `global.json` equivalent from `Scrapers` project target).
- PostgreSQL 15+ running locally (e.g., `brew install postgresql@18 && brew services start postgresql@18`).
- If you previously created `clinical_trials` or `ClinicalTrialData` databases for this project, remove them (`dropdb clinical_trials`, `dropdb ClinicalTrialData`) to avoid stale data.

Create the `clinical_trial_data` database:

```bash
createdb clinical_trial_data
```

Export the connection string (use your local user/password as needed):

```bash
export POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Database=clinical_trial_data;Username=$USER"
```

## Programmatic Ingestion

Ingestion is exposed as composable services so future cron jobs (or your own console app) can orchestrate them. Example:

```csharp
var client = new ClinicalTrialsGov();
var repository = new PostgresStudyRepository(Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!);
var coordinator = new ClinicalTrialsIngestionService(client, repository);
await coordinator.IngestAsync(count: 5);
```

This fetches the first five studies from the `/studies` endpoint and persists them into the `studies` and `investigators` tables.

Verify the rows with `psql`:

```bash
psql $POSTGRES_CONNECTION_STRING -c "SELECT COUNT(*) FROM studies;"
psql $POSTGRES_CONNECTION_STRING -c "SELECT COUNT(*) FROM investigators;"
```

## Tests

All tests (unit + integration) must pass locally before opening a PR:

```bash
POSTGRES_CONNECTION_STRING=... dotnet test
```

The PostgreSQL-backed tests rely on the connection string above and will create the `studies` and `investigators` tables if they do not already exist.
