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

# Apply database migrations (required on first run)
dotnet ef database update --project Scrapers/
```

## Run

```bash
# Start the API (port 5003) — applies pending migrations on startup
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

Services deploy to a single DigitalOcean Droplet via the Docker deployment workflow.

### Docker Deployment

Trigger `.github/workflows/deploy-docker.yml` manually from **Actions**. The workflow
builds and pushes the DataApi, Frontend, and IngestionApp images, applies migrations,
then starts all services under `/opt/clinicaltrialdata/`.

Caddy runs alongside the application containers with host networking. It terminates
TLS for `https://clinaxis.org` and `https://www.clinaxis.org`, proxies `/api/*` to
DataApi, and proxies the Frontend and Blazor Server SignalR traffic to port 5001.
Certificate state is retained in Docker volumes so Caddy can renew certificates
automatically.

### Droplet Services

```bash
sudo docker compose --env-file /opt/clinicaltrialdata/.env \
  -f /opt/clinicaltrialdata/docker-compose.yml ps
sudo docker logs clinicaltrialdata-caddy --tail 200
sudo docker logs clinicaltrialdata-api --tail 200
```

### Health Checks

- **Public Frontend**: `GET https://clinaxis.org/health` → `200 OK`
- **Public DataApi**: `GET https://clinaxis.org/api/data-source-state` → `200 OK`
- **IngestionApp**: Background service, no health endpoint

For details on the deployment architecture, see `docs/scraper_architecture.md` → "Architecture Decisions" section.

## Advanced Search

Search studies across 6 dimensions: keyword, status, phase, conditions, enrollment range, and start date range. Filters combine with AND logic (all must match), supporting complex queries like "RECRUITING + PHASE2 + Hypertension studies with 100-500 participants."

API endpoints for building custom search UIs:
- `GET /api/distinct-conditions` — List all conditions
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
