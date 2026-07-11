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

## Deployment

The system runs on a single DigitalOcean Droplet:

- **PostgreSQL**: Docker container on the droplet (or managed DO PostgreSQL)
- **DataApi**: `dotnet publish --self-contained -r linux-x64` → SCP → systemd service. Listens on localhost:5003.
- **Frontend**: Same publish/SCP/systemd pattern. **Kestrel serves HTTPS directly on port 80/443.** TLS via .NET's built-in HTTPS + Let's Encrypt cert.
- **IngestionApp**: Runs via cron or systemd timer for scheduled data ingestion
- **No nginx.** Keep the stack minimal.

## Ports

| Service   | Port | Notes |
|-----------|------|-------|
| Frontend  | 80/443 | Public entry point. Kestrel directly, TLS via .NET HTTPS + Let's Encrypt. |
| DataApi   | 5003 | Internal, not exposed publicly. Frontend calls DataApi via HttpClient. |

(5000 is reserved by macOS AirPlay Receiver / Control Center.)

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
