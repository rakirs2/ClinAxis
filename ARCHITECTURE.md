# Architecture

## Overview

ClinicalTrialData fetches studies from ClinicalTrials.gov and PubMed, stores them in PostgreSQL, exposes them via a REST API, and provides a Blazor Server frontend for browsing.

## Project Layout

```
ClinicalTrialData/
├── Scrapers/               # Core library: entities, repositories, scraping services
├── Scrapers/Testing/       # Shared test utilities (DbTestBase, SnapshotDb, SeedData)
├── DataApi/                # ASP.NET Core Minimal API (port 5003)
├── Frontend/               # Blazor Server UI (port 5001)
├── IngestionApp/           # Console app for running the full pipeline
├── Scrapers.Tests/         # Unit tests with fake HTTP handlers
├── Scrapers.IntegrationTests/  # Live API + PostgreSQL integration tests
├── Frontend.Tests/         # bUnit + MockHttp tests for Blazor pages
└── .opencode/plans/        # Decision log and execution plans for agents
```

## Data Flow

```
ClinicalTrials.gov  ──►  ClinicalTrialsGovService  ──►  PostgreSQL
       │                                                      ▲
PubMed  ───────────►  PubMedScraperService  ──────────────────┤
                                                               │
                           AggregationService  ────────────────┤
                                                               │
                           DataApi  ───────────────────────────┘
                               │
                               │  (HTTP only — no direct DB access)
                               │
                           Frontend (Blazor Server, Interactive Server)
                                   (also calls DataApi, never the DB directly)
```

**Critical rule: Only DataApi reads/writes to PostgreSQL.** Frontend, IngestionApp, and tests all go through DataApi's REST API or share the `Scrapers` library (which DataApi also uses). No DbContext or SQL outside DataApi.

## Deployment Architecture

ClinicalTrialData uses independent service deployments to allow redeploying any service (DataApi, Frontend, or IngestionApp) without interrupting the others on the same DigitalOcean instance.

### Problem & Solution

**Problem**: All three services running on a single droplet means redeploying the scraper service would require stopping the API and Frontend, creating unnecessary downtime.

**Solution**: Three independent GitHub Actions workflows with path-based triggers ensure only the changed service redeploys:

```
┌─────────────────────────────────────────────────────────────┐
│              GitHub Actions Workflows                       │
├────────────────┬──────────────────┬───────────────────────┤
│ deploy-dataapi │ deploy-frontend  │ deploy-ingestion      │
│ (DataApi/**)   │ (Frontend/**)    │ (IngestionApp/**)     │
└────────┬───────┴────────┬─────────┴──────────┬────────────┘
         │                │                    │
         └────────────────┼────────────────────┘
                          │
                      SSH to Droplet
                          │
         ┌────────────────┼────────────────────┐
         │                │                    │
    ┌────▼────┐      ┌────▼────┐         ┌────▼──────┐
    │ systemd │      │ systemd │         │ systemd   │
    │  API    │      │Frontend │         │Ingestion  │
    │ :5003   │      │ :5001   │         │(bg job)   │
    └─────────┘      └─────────┘         └───────────┘
         │                │                    │
         └────────────────┼────────────────────┘
                          │
                      Nginx Proxy
                    (port 80/443)
```

### Key Benefits

- **Independent restarts**: Scraper redeploy doesn't affect API/Frontend
- **Faster deployments**: Only changed service rebuilds and redeploys
- **Reduced risk**: One service failing doesn't cascade to others
- **Simple rollback**: Revert and re-run workflow for failed service
- **Parallel deployments**: Multiple services can deploy simultaneously

## Production Infrastructure

ClinicalTrialData runs on a single DigitalOcean Ubuntu droplet with the following architecture:

### Droplet Configuration

- **Instance**: Single DigitalOcean Droplet (Ubuntu)
- **Services**: Three independent .NET processes managed by systemd
- **Reverse Proxy**: Nginx (routes external traffic to backend services on ports 80/443)
- **Database**: PostgreSQL (local instance, shared by all services)

### Service Ports

| Service | Port | Role |
|---------|------|------|
| DataApi | 5003 | REST API for study data (internal, proxied by nginx) |
| Frontend | 5001 | Blazor Server UI (internal, proxied by nginx) |
| IngestionApp | - | Background scraper service (no exposed port) |
| PostgreSQL | 5432 | Database (localhost only, not exposed) |

### Systemd Services

Each service runs as a systemd service for automatic restart and lifecycle management:

- `clinicaltrialdata-api.service` — DataApi (ASP.NET Core)
- `clinicaltrialdata-frontend.service` — Frontend (Blazor Server)
- `clinicaltrialdata-ingestion.service` — IngestionApp (BackgroundServices)

Services can be managed independently:
```bash
sudo systemctl restart clinicaltrialdata-api     # Restart only DataApi
sudo systemctl status clinicaltrialdata-*        # Check all services
sudo journalctl -u clinicaltrialdata-api -f      # Live logs
```

### Deployment Directories

```
/opt/clinicaltrialdata/
├── api/                    # DataApi binaries
├── frontend/               # Frontend binaries
├── ingestion/              # IngestionApp binaries
└── releases/               # Version history (optional)
```

### GitHub Actions Integration

Deployments are triggered automatically via three GitHub Actions workflows:
- **deploy-dataapi.yml** — Triggers on changes to `DataApi/**` or manual dispatch
- **deploy-frontend.yml** — Triggers on changes to `Frontend/**` or manual dispatch
- **deploy-ingestion.yml** — Triggers on changes to `IngestionApp/**` or manual dispatch

Each workflow:
1. Checks out code and builds in Release mode (`dotnet publish --self-contained -r linux-x64`)
2. Publishes binary to deployment directory via SSH
3. Restarts the corresponding systemd service
4. Verifies service health before completing

### Authentication & Secrets

Droplet authentication is configured via GitHub Secrets:
- `DEPLOY_HOST` — Droplet IP address or hostname
- `DEPLOY_USER` — SSH user (ubuntu or root)
- `DEPLOY_SSH_KEY` — Private SSH key for authentication
- `PROD_DB_CONNECTION` — PostgreSQL connection string for production database

### Database

PostgreSQL runs on the same instance (`localhost:5432`) and is shared by all three services. The connection string is passed via environment variable `PROD_DB_CONNECTION` (set in systemd service files).

All three services connect to the same `clinical_trial_data` database, with the IngestionApp performing schema migrations on startup if needed.

## API Endpoints

| Method | Path                        | Description                   |
|--------|-----------------------------|-------------------------------|
| GET    | /api/studies                | Paginated list with filters   |
| GET    | /api/studies/{nctId}        | Single study detail           |
| GET    | /api/pipeline-runs          | Pipeline execution history    |
| GET    | /api/stats                  | Aggregate counts              |
| GET    | /api/telemetry              | Full system telemetry         |
| GET    | /api/aggregations           | PI + category aggregation data|

## Test Infrastructure

Three database testing modes, all using `Testcontainers.PostgreSql` (disposable Docker PostgreSQL):

| Mode | Class | Lifecycle | Use |
|------|-------|-----------|-----|
| Integration/IO | `DbTestBase` | Testcontainers container per class, transaction rollback per method | Fast integration tests |
| Snapshot | `SnapshotDb` | Container seeded with golden data | Deterministic assertions |
| Persistent | `SnapshotDb(persist:true)` | Fixed DB name, survives test run | Manual inspection |

Test utilities live in `Scrapers/Testing/` and are shared via `InternalsVisibleTo`. No duplication.

## Key Technologies

- **.NET 10** — target framework
- **Blazor Server with Interactive Server** — frontend with SignalR real-time capability
- **Entity Framework Core 10.x** — schema migrations via code
- **PostgreSQL 15+** — persistence via Npgsql
- **bUnit** — Blazor component testing
- **MSTest** — test framework
- **Bootstrap 5** — UI styling
