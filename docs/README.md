# Documentation

This folder contains important guidance and technical documentation for the ClinicalTrialData project.

## Quick Start for Contributors

Read these in order:

1. **[AGENTS.md](/AGENTS.md)** - Core rules and requirements for all contributions
   - One feature per PR, small PRs, all tests must pass locally
   - Tech stack (C# .NET 10, Blazor, EF Core, PostgreSQL)
   - Database testing modes (Integration/IO, Snapshot, Persistent)
   - Zero data loss principle: all API fields must be persisted or explicitly documented

2. **[scraper_architecture.md](./scraper_architecture.md)** - Full scraper architecture, entity model, pipeline design, and field coverage matrix
   - Covers all data sources, event system, and architecture decisions
   - Includes the complete field coverage matrix (what's persisted and what's not)
   - Read this before making any code changes to the scraper

3. **[data_loss_remediation.md](./data_loss_remediation.md)** - Active work on completing data persistence
   - 22 fields from ClinicalTrials.gov API are missing from database
   - PR #58 fixed Locations (1 of 7 categories)
   - This file tracks what's left, prioritization, and effort estimates
   - Read this before starting any data persistence work

## Project Structure

```
ClinicalTrialData/
├── Scrapers/                       # Core business logic (scraping, entities, repositories, migrations)
│   ├── Coordinators/               # Pipeline orchestration (IngestionCoordinator, PipelineRunner)
│   ├── Models/                     # API response models (ClinicalTrialsGov request/response)
│   ├── Persistence/
│   │   ├── Entities/               # EF Core entity definitions
│   │   ├── Migrations/             # EF Core database migrations
│   │   └── StudyRepository.cs      # Data access layer
│   ├── Services/
│   │   ├── ClinicalTrialsIngestionService.cs  # CT.gov ingestion orchestration
│   │   ├── PubMedScraperService.cs            # PubMed paper fetching
│   │   ├── AggregationService.cs              # PI + category computation
│   │   ├── CrawlServices/          # Pivot enricher framework (IPivotEnricherService)
│   │   └── EventQueue/             # Event queue + data source tracking services
│   ├── Testing/                    # Shared test utilities (DbTestBase, SnapshotDb)
│   └── Scrapers.csproj
├── DataApi/                        # ASP.NET Core Minimal API (port 5003)
├── Frontend/                       # Blazor Server UI (port 5001)
├── IngestionApp/                   # Background service: event-driven scraping pipeline
│   ├── ClinicalTrialsScrapeService.cs    # Periodic CT.gov scrape → enqueue events
│   ├── EventProcessingService.cs         # Claim + process pipeline events
│   └── DeadLetterProcessingService.cs    # Monitor failed events
├── deploy/                         # DigitalOcean Droplet setup (systemd units, setup.sh)
├── Scrapers.Tests/                 # Unit tests with fake HTTP handlers + captured payloads
├── Scrapers.IntegrationTests/      # Live API + PostgreSQL integration tests
├── Frontend.Tests/                 # bUnit + MockHttp tests for Blazor pages
├── docs/                           # Documentation
├── AGENTS.md                       # Contribution guidelines
└── .editorconfig                   # Code style rules (non-negotiable)
```

## Key Principles

### One Feature, One PR
- Branch from `origin/main`
- Every PR is a single logical change
- Small enough to review in under 5 minutes
- All tests must pass locally before creating PR

### All Tests Run Locally
- Tests use Testcontainers + real PostgreSQL
- No `[Ignore]` attributes - all tests run
- Run `dotnet test` and all must pass
- Docker Desktop must be running (Testcontainers manages containers)

### Single Database Gateway
- **Only DataApi talks to PostgreSQL**
- Frontend, IngestionApp, and tests access database via DataApi REST endpoints or `Scrapers` library repositories
- This preserves a single contract boundary and prevents tight coupling

### Zero Data Loss
- If an external API returns a field, it MUST be persisted to PostgreSQL
- Or document WHY it's intentionally ignored
- See [data_loss_remediation.md](./data_loss_remediation.md) for active audit

## Testing

Three database testing modes (all via Testcontainers):

| Mode | Class | Use Case |
|------|-------|----------|
| **Integration/IO** | `DbTestBase` | Testcontainers container per class, transaction rollback per method. Fast and isolated. |
| **Snapshot** | `SnapshotDb` | Container seeded with known golden data. Deterministic assertions against fixed dataset. |
| **Persistent/fiddle** | `SnapshotDb(persist: true)` | Same as snapshot but no rollback — DB stays for manual inspection. |

Run all tests:
```bash
dotnet build        # Must have 0 errors, 0 warnings
dotnet test         # All tests must pass
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Language | C# .NET 10 |
| Frontend | Blazor Server |
| ORM | EF Core 10.x + Npgsql 9.x |
| Database | PostgreSQL 15+ (Docker locally) |
| Testing | MSTest only (no xUnit, no NUnit) |
| CI/CD | GitHub Actions |
| Deploy | DigitalOcean Droplet + systemd + Kestrel |

## Contribution Workflow

1. Fetch latest main: `git fetch origin main && git checkout origin/main -b feature/<name>`
2. Build and test: `dotnet build && dotnet test`
3. Make changes to ONE feature only
4. Commit locally: `git commit -m "feat: <description>"`
5. Push: `git push origin feature/<name>`
6. Create PR on GitHub (title should match commit message)
7. Request review
8. **Do not merge without approval** - CI must pass + human review required

## Database Operations

**Truncate all scraped data** (keeps schema + scraper config):
```bash
psql -d clinical_trial_data -f scripts/truncate-local-db.sql
# or via env var:
psql "$POSTGRES_CONNECTION_STRING" -f scripts/truncate-local-db.sql
```

**Full validation pipeline** (truncate + rescrape + validate 3000 studies):
```bash
POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Database=clinical_trial_data;Username=<user>" \
  dotnet run --project Scrapers.Validation -- 3000 --truncate
```

## Code Style

All style rules are in `.editorconfig` - this is the single source of truth. If a rule fires, fix the code—do not suppress it. No `#pragma` directives, no `<NoWarn>` in `.csproj` files.

## Questions?

- Contribution guidelines → See `AGENTS.md`
- Scraper architecture, entity model, pipeline design → See `scraper_architecture.md`
- Data persistence work → See `data_loss_remediation.md`
- Build/test issues → Ensure Docker Desktop is running, then `dotnet test`
