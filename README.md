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
var repository = new StudyRepository(Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!);
var coordinator = new ClinicalTrialsIngestionService(client, repository);
await coordinator.IngestAsync(count: 5);
```

This fetches the first five studies from the `/studies` endpoint and persists them into the `studies` and `investigators` tables.

### Schema & Migrations

Entity Framework Core manages the schema (see `Scrapers/Persistence/Migrations`). When you change the schema, add a migration via `dotnet ef migrations add <Name> --project Scrapers` and run `dotnet ef database update` (or rely on the ingestion service/tests calling `Database.Migrate`).

Verify the rows with `psql`:

```bash
psql $POSTGRES_CONNECTION_STRING -c "SELECT COUNT(*) FROM studies;"
psql $POSTGRES_CONNECTION_STRING -c "SELECT COUNT(*) FROM investigators;"
```

## Tests

Run unit tests (no Postgres dependency):

```bash
dotnet test Scrapers.Tests/Scrapers.Tests.csproj
```

Run integration tests (requires local Postgres + live ClinicalTrials.gov access):

```bash
POSTGRES_CONNECTION_STRING=... dotnet test Scrapers.IntegrationTests/Scrapers.IntegrationTests.csproj
```

The integration suite truncates the database between runs, runs migrations automatically, and verifies that live API calls persist data.
