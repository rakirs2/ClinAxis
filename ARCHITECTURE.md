# Architecture

## Overview

ClinicalTrialData fetches studies from ClinicalTrials.gov and PubMed, stores them in PostgreSQL, exposes them via a REST API, and provides a Blazor Server frontend for browsing.

## Project Layout

```
ClinicalTrialData/
├── Scrapers/               # Core library: entities, repositories, scraping services
├── DataAggregators/        # PI & category aggregation logic
├── DataApi/                # ASP.NET Core Minimal API (port 5003)
├── Frontend/               # Blazor Server UI (port 5001)
├── Scrapers.Tests/         # Unit tests with fake HTTP handlers
├── Scrapers.IntegrationTests/  # Live API + PostgreSQL integration tests
├── DataAggregators.Tests/  # Aggregation unit tests
├── Frontend.Tests/         # bUnit + MockHttp tests for Blazor pages
└── IngestionApp/           # Console app for running the full pipeline
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
                          Frontend (Blazor Server)
```

## Deployment

The system is designed to run on two DigitalOcean droplets:

### Droplet 1 — Database
- PostgreSQL 15+
- Single `clinical_trial_data` database
- Configured to accept connections from Droplet 2's IP

### Droplet 2 — Application
- .NET 10 runtime
- DataApi (port 5003) behind nginx reverse-proxy to port 80/443
- Frontend (port 5001) behind same nginx
- Systemd units for DataApi and Frontend
- Daily cron for ingestion via `IngestionApp`

## Ports

| Service   | Port |
|-----------|------|
| DataApi   | 5003 |
| Frontend  | 5001 |

(5000 is reserved by macOS AirPlay Receiver / Control Center.)

## API Endpoints

| Method | Path                        | Description                   |
|--------|-----------------------------|-------------------------------|
| GET    | /api/studies                | Paginated list with filters   |
| GET    | /api/studies/{nctId}        | Single study detail           |
| GET    | /api/pipeline-runs          | Pipeline execution history    |
| GET    | /api/stats                  | Aggregate counts              |

## Key Technologies

- **.NET 10** — target framework
- **Entity Framework Core** — schema migrations
- **PostgreSQL** — persistence
- **bUnit** — Blazor component testing
- **MSTest** — test framework
- **Bootstrap 5** — UI styling
