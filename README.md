# ClinicalTrialData

Fetch and persist structured snapshots from [ClinicalTrials.gov](https://clinicaltrials.gov) and [PubMed](https://pubmed.ncbi.nlm.nih.gov/). Browse studies, investigators, and PubMed papers through a web UI.

## Requirements

- .NET 10 SDK
- PostgreSQL 15+ running locally (e.g., `brew install postgresql@18 && brew services start postgresql@18`)

## Setup

```bash
# Create the database
createdb clinical_trial_data

# Export connection string
export POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Database=clinical_trial_data;Username=$USER"

# Build everything
dotnet build
```

## Run

```bash
# Start the API (port 5003)
dotnet run --project DataApi/

# Start the frontend (port 5001) — separate terminal
dotnet run --project Frontend/
```

Open http://localhost:5001 in your browser.

> Port 5000 is reserved by macOS AirPlay Receiver / Control Center. DataApi uses port 5003 to avoid the conflict.

## Ingest Data

```bash
dotnet run --project DataAggregators/ -- --count 50
```

## Tests

```bash
# Unit tests (no Postgres needed)
dotnet test Scrapers.Tests/
dotnet test Frontend.Tests/

# All tests (requires local Postgres)
dotnet test
```

Integration tests connect to a live Postgres instance and real ClinicalTrials.gov/PubMed APIs. They truncate the database before each run and perform a fresh ingestion of 5 studies.

## Projects

| Project | Description |
|---------|-------------|
| `Scrapers/` | Core library: entities, repositories, ClinicalTrials.gov & PubMed scrapers |
| `DataAggregators/` | PI and category aggregation pipeline step |
| `DataApi/` | ASP.NET Core Minimal API (4 endpoints) |
| `Frontend/` | Blazor Server UI (Search, Study Detail, Pipeline History) |
| `IngestionApp/` | Console app to run the full pipeline |
| `Scrapers.Tests/` | Unit tests with fake HTTP handlers and captured payloads |
| `Scrapers.IntegrationTests/` | Live integration tests (API + Postgres) |
| `Frontend.Tests/` | bUnit component tests with HTTP mocking |

## Tech Stack

- .NET 10 · ASP.NET Core · Entity Framework Core · PostgreSQL
- Blazor Server · Bootstrap 5 · bUnit · MSTest · RichardSzalay.MockHttp
