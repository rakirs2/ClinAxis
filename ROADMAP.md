# ClinicalTrialData Roadmap

## Overview

This document outlines all planned and completed PRs for the MVP. Each PR is a single logical change, built and verified locally before merging.

## Completed PRs (Original Build)

These PRs built the initial codebase. No further changes should be needed to them.

- **PR 1** — PipelineRun entity + Abstract column + migration (`feature/pubmed-scraper-fresh`, #6)
- **PR 2** — Strict linter + connection string centralization (`feature/linter-and-connection-strings`, #17)
- **PR 3** — PI & category aggregations (`feature/pi-category-aggregations`, #19)
- **PR 4** — DataApi project (`feature/data-api`, #20)
- **PR 5** — Frontend Blazor Server project (`feature/frontend`, #21)
- **PR 6** — Frontend tests (`feature/frontend-tests`, #22)
- **PR 7** — Architecture docs + final polish (unmerged, content absorbed into current docs)

## Upcoming PRs (Restoration & Features)

### PR 0: Agent Guidelines & Project Vision (CURRENT)
**Goal:** Update all documentation so every future agent knows how to operate.

- Rewrite `AGENTS.md` with core priorities, tech stack lock, three DB testing modes, workflow rules
- Update `README.md` — Docker-first quickstart, fix stale `DataAggregators/` references
- Update `ARCHITECTURE.md` — single-droplet deployment model, test infrastructure
- Update `ROADMAP.md` — reflect new PR structure

**Verification:** `dotnet build` (0 errors, 0 warnings) — no code changes, just docs.

---

### PR 1: Build Fix + Package Upgrades + Test Infrastructure
**Goal:** `dotnet build` + `dotnet test` passing. Tests work with Docker-only Postgres.

- Add `Testcontainers.PostgreSql` NuGet package to `Scrapers.csproj`
- Create `Scrapers/Testing/` with `DbTestBase` (Testcontainers + txn rollback), `SnapshotDb` (seeded golden data), `SeedData`
- Upgrade EF Core 8.0.4 → 10.x, Npgsql 8.0.4 → 9.x across all `.csproj` files
- De-duplicate test utilities into `Scrapers/Testing/`, shared via `InternalsVisibleTo`
- Delete old `EphemeralPostgresDatabase` files (4 files, 2x duplicated)
- Add `docker-compose.yml` (Postgres only for manual use), `appsettings.json` files

**Verification:** `dotnet build` (0 errors/warnings), `dotnet test` (all pass — Testcontainers manages PostgreSQL automatically)

---

### PR 2: Feature 1 — Display Studies from Database
**Goal:** Home page shows paginated studies from DB. First working feature.

- Switch Frontend to Interactive Server (`AddInteractiveServerComponents`), enabling SignalR
- Rewrite `Home.razor` as proper Blazor component (no vanilla JS)
- Paginated study list with search

**Verification:** Navigate to `/` → see studies with pagination. `dotnet test` passes.

---

### PR 3: Feature 2 — Principal Investigators Page
**Goal:** New `/investigators` page listing all PIs.

- Add `GET /api/investigators` endpoint
- Add repository method for PI list with study counts
- New `Investigators.razor` page with nav link

**Verification:** `/investigators` lists all PIs. `dotnet test` passes.

---

### PR 4: Feature 3 — Advanced Search Page
**Goal:** Search with all filter criteria (status, phase, condition, keyword, enrollment, dates).

- Extend `GET /api/studies` with additional filter params
- Create `Search.razor` with multi-filter UI
- Update repository query methods

**Verification:** All filter combinations work. `dotnet test` passes.

---

### PR 5: Feature 4 — Status Page with Real-Time Updates
**Goal:** `/status` shows live DB & scraper stats via Blazor's SignalR circuit.

- Enhance `GET /api/telemetry` with full pipeline + event data
- Timer-driven auto-refresh on `Status.razor`

**Verification:** `/status` auto-updates without page refresh. `dotnet test` passes.

---

### PR 6: GitHub Actions Deploy → DigitalOcean Droplet
**Goal:** Push to `main` → build + test + deploy + verify deployed services.

- `dotnet publish --self-contained` in CI
- SCP to DO Droplet
- systemd service management
- Add `deploy/` directory with configs and setup script
- **Deploy pipeline runs all integration tests first** — if tests fail, deploy is blocked
- **Post-deploy smoke tests** — CI calls live deployed Frontend (port 80/443) and verifies HTTP 200 + correct response shape (DataApi is internal, not publicly exposed)
- **Rollback on failure** — if smoke tests fail, CI restores previous version and alerts

**Verification:** 
- `dotnet test` passes in CI
- After deploy, CI curls `https://<droplet>/` and expects HTTP 200 (Frontend loads, Blazor Server serves interactivity via SignalR)
- All tests run as GitHub Actions checks — nothing merges without green CI

---

## Future Considerations

- **DataGenService** — Rename `IngestionApp` to a background hosted service for scheduled scraping
- **CMS Medicare cross-reference** — Map PIs to CMS provider data via NPPES NPI Registry
- **Any Docker-based deployment path** (deliberately deferred — see AGENTS.md section 7)

---

## Development Flow

1. **1 feature, 1 branch, 1 PR.** Every new feature gets its own branch from `origin/main`.
2. **All work on feature branches** from `origin/main` — never from stale local `main`.
3. **No merging until local verification**: `dotnet build` (0 errors/warnings) + `dotnet test` (all pass)
4. **Review existing plans**: Check `.opencode/plans/` for any relevant context before starting
5. **Document attempts**: Update `.opencode/plans/PLAN.md` with what was tried and the outcome
