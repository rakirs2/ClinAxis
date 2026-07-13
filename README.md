# ClinicalTrialData

Fetch and persist structured snapshots from [ClinicalTrials.gov](https://clinicaltrials.gov) and [PubMed](https://pubmed.ncbi.nlm.nih.gov/). Browse studies, investigators, and PubMed papers through a web UI.

## Requirements

- .NET 10 SDK
- Docker (for PostgreSQL — no local Postgres installation needed)

## Setup

```bash
# Start PostgreSQL via Docker
docker compose up -d postgres

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

In development, the DataApi seeds test data automatically on startup (non-Production environments only).

For production ingestion, the IngestionApp runs as a background service with an event-driven pipeline:

```bash
export POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Database=clinical_trial_data;Username=postgres;Password=postgres"
dotnet run --project IngestionApp/
```

The IngestionApp:
- Periodically fetches studies from ClinicalTrials.gov
- Enqueues discovery events for downstream processing
- Scrubs investigator publication records via PubMed

See `docs/scraper_architecture.md` for the full pipeline design.

## Tests

```bash
# Unit tests (no Postgres needed)
dotnet test Scrapers.Tests/
dotnet test Frontend.Tests/

# All tests (Testcontainers manages Docker PostgreSQL automatically)
dotnet test
```

Integration tests connect to a real PostgreSQL via Testcontainers and real ClinicalTrials.gov/PubMed APIs. No manual Docker setup needed — just have Docker Desktop running.

## Deployment to Production

Services deploy independently to a single DigitalOcean Droplet via GitHub Actions:

### Automatic Deployment

Push code changes to `main` and GitHub Actions automatically deploys only the changed service:

- **DataApi** changes (`DataApi/**`) → `deploy-dataapi.yml` runs
- **Frontend** changes (`Frontend/**`) → `deploy-frontend.yml` runs  
- **IngestionApp** changes (`IngestionApp/**` or `Scrapers/**`) → `deploy-ingestion.yml` runs

Each workflow:
1. Builds the service with `dotnet publish --self-contained -r linux-x64`
2. SSHes to the droplet using GitHub Secrets (`DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`)
3. Deploys binaries to `/opt/clinicaltrialdata/{service}/`
4. Restarts only the corresponding systemd service
5. Validates health before completing

### Manual Deployment

Trigger deployment manually via GitHub UI:
- Go to **Actions** → select **Deploy DataApi** (or Frontend/IngestionApp)
- Click **Run workflow** → **Run workflow**

### Systemd Services on Droplet

```bash
sudo systemctl restart clinicaltrialdata-api      # DataApi
sudo systemctl restart clinicaltrialdata-frontend # Frontend
sudo systemctl restart clinicaltrialdata-ingestion # IngestionApp
sudo systemctl status clinicaltrialdata-*         # Check all
sudo journalctl -u clinicaltrialdata-api -f       # View logs
```

### Health Checks

- **DataApi**: `GET /health` → `200 OK` with `{"status":"healthy"}`
- **Frontend**: Any `200` response on `GET /` indicates health
- **IngestionApp**: Background service, no health endpoint

For details on the deployment architecture, see `docs/scraper_architecture.md` → "Architecture Decisions" section.

## Advanced Search

Search studies across 7 dimensions: keyword, status, phase, conditions, locations (country/state/city/facility), enrollment range, and start date range. Filters combine with AND logic (all must match), supporting complex queries like "RECRUITING + PHASE2 + Hypertension studies in the USA with 100-500 participants."

API endpoints for building custom search UIs:
- `GET /api/distinct-conditions` — List all conditions
- `GET /api/distinct-locations` — List countries, states, cities, facilities
- `GET /api/studies?keyword=&status=&phase=&condition=&country=&enrollmentMin=&enrollmentMax=&startDateFrom=` — Search with filters

See `DataApi/Program.cs` for implementation details. Frontend UI code in `Frontend/Pages/Search.razor`.

## Projects

| Project | Description |
|---------|-------------|
| `Scrapers/` | Core library: entities, repositories, ClinicalTrials.gov & PubMed scrapers, shared test utilities |
| `DataApi/` | ASP.NET Core Minimal API (6 endpoints) |
| `Frontend/` | Blazor Server UI (Search, Study Detail, Pipeline History, Status) |
| `IngestionApp/` | Console app to run the full pipeline |
| `Scrapers.Tests/` | Unit tests with fake HTTP handlers and captured payloads |
| `Scrapers.IntegrationTests/` | Live integration tests (API + Postgres) |
| `Frontend.Tests/` | bUnit component tests with HTTP mocking |

## Tech Stack

- .NET 10 · ASP.NET Core · Entity Framework Core · PostgreSQL (Docker)
- Blazor Server (Interactive Server) · Bootstrap 5 · bUnit · MSTest · RichardSzalay.MockHttp
