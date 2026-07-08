# ClinicalTrialData
Fetch and persist structured snapshots from [ClinicalTrials.gov](https://clinicaltrials.gov) so investigators and studies can be analyzed locally.

## Requirements
- .NET 10 SDK (ships with this repo via `global.json` equivalent from `Scrapers` project target).
- PostgreSQL 15+ running locally (e.g., `brew install postgresql@18 && brew services start postgresql@18`).
- If you previously created a `clinical_trials` database for this project, remove it (`dropdb clinical_trials`) to avoid stale data.

Create the `ClinicalTrialData` database:

```bash
createdb ClinicalTrialData
```

Export the connection string (use your local user/password as needed):

```bash
export POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Database=ClinicalTrialData;Username=$USER"
```

## Ingest the First 5 Studies

```bash
POSTGRES_CONNECTION_STRING=... dotnet run --project IngestionApp -- 5
```

The command fetches the first *n* (default 5) studies from the `/studies` endpoint, persists them into `studies` / `investigators` tables, and prints the total persisted count.

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
