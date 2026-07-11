# Documentation

This folder contains important guidance and technical documentation for the ClinicalTrialData project.

## Quick Start for Contributors

Read these in order:

1. **[AGENTS.md](/AGENTS.md)** - Core rules and requirements for all contributions
   - One feature per PR, small PRs, all tests must pass locally
   - Tech stack (C# .NET 10, Blazor, EF Core, PostgreSQL)
   - Database testing modes (Integration/IO, Snapshot, Persistent)
   - Zero data loss principle: all API fields must be persisted or explicitly documented

2. **[data_loss_remediation.md](./data_loss_remediation.md)** - Active work on completing data persistence
   - 22 fields from ClinicalTrials.gov API are missing from database
   - PR #58 fixed Locations (1 of 7 categories)
   - This file tracks what's left, prioritization, and effort estimates
   - Read this before starting any data persistence work

## Project Structure

```
ClinicalTrialData/
├── Scrapers/                       # Core business logic (repositories, entities, migrations)
│   ├── Persistence/
│   │   ├── Entities/               # EF Core entity definitions
│   │   ├── Migrations/             # EF Core database migrations
│   │   └── StudyRepository.cs      # Data access layer
│   ├── Testing/                    # Shared test utilities
│   └── Scrapers.csproj
├── DataApi/                        # REST API (only way to access database)
├── Frontend/                       # Blazor Server frontend
├── docs/                           # This folder - documentation
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

## Code Style

All style rules are in `.editorconfig` - this is the single source of truth. If a rule fires, fix the code—do not suppress it. No `#pragma` directives, no `<NoWarn>` in `.csproj` files.

## Questions?

- Contribution guidelines → See `AGENTS.md`
- Data persistence work → See `data_loss_remediation.md`
- Architecture questions → See structure above and test patterns in `Scrapers/Testing/`
- Build/test issues → Ensure Docker Desktop is running, then `dotnet test`
