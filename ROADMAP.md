# ClinicalTrialData Roadmap

## Overview

This document outlines all planned PRs for the MVP. Each PR is tied to a GitHub issue. Future agents should read this roadmap, the relevant `.opencode/plans/` files for context, and the matching issue before implementing.

## Issues & PRs

### Issue #10 — PR 1: PipelineRun entity + Abstract column + migration (DONE)

**Branch:** `feature/pubmed-scraper-fresh`
**PR:** #6

**Changes:**
- `PipelineRunEntity` — tracks each pipeline execution
- `Abstract` column on `pubmed_studies` for hover tooltip
- Migration `AddPipelineRunsAndAbstract`
- `StudyRepository` — 6 new methods
- `PubMedScraperService` — extracts `<AbstractText>` from EFetch XML
- `PipelineRunner` — creates/records pipeline run per execution

**Verified:** `FiftyTrialMappingIntegrationTest` passes, `dotnet build` 0 errors

---

### Issue #11 — PR 2: Strict linter + connection string centralization

**Goal:** One PR that makes style enforcement automatic forever, and removes connection string duplication.

**Changes:**
1. `.editorconfig` at repo root — all Microsoft C# style/naming rules at `error` severity
2. `Directory.Build.props` — `TreatWarningsAsErrors=true`, `AnalysisMode=All`, `EnforceCodeStyleInBuild=true`
3. Run `dotnet format --severity error` to auto-fix ~200 issues
4. Manual fixes: rename `s_` static fields, dispose pattern on `ClinicalTrialsGov`, fix `PipelineRunner` catch
5. `ConnectionStringProvider.cs` — single shared fallback for all projects
6. Update 6 files to use the provider

**Verification:**
- `dotnet build` — 0 errors, 0 warnings
- `dotnet test` — all pass
- Rider opens with no squiggles

---

### Issue #12 — PR 3: PI & category aggregations (DONE)

**Branch:** `feature/pi-category-aggregations`
**PR:** #19

**Goal:** Compute counts and links by PI and category, stored in the DB for fast queries.

**Changes:**
- `PiAggregationEntity` — per investigator: study count, PubMed paper count, linked NCT IDs
- `CategoryAggregationEntity` — per condition/keyword/phase: study count, PubMed paper count, linked NCT IDs
- `AggregationService` — aggregates all data after ClinicalTrials + PubMed scrape
- `StudyRepository` — `GetAllStudiesWithFullDataAsync`, `ReplacePiAggregationsAsync`, `ReplaceCategoryAggregationsAsync`, `GetPubmedStudiesWithAuthorsAsync`
- `PipelineRunner` — calls `AggregationService.AggregateAsync()` after PubMed scrape
- Migration `AddAggregationTables` — creates `pi_aggregations` and `category_aggregations` tables
- Schema guard tests for both new tables
- Integration tests for aggregation logic

**Verified:**
- `dotnet build` — 0 errors, 0 warnings
- `dotnet test` — 21/21 pass (6 unit + 15 integration)

---

### Issue #13 — PR 4: DataApi project (DONE)

**Branch:** `feature/data-api`
**PR:** #20

**Goal:** Expose scraped data via REST API.

**Changes:**
- New `DataApi/` project (ASP.NET Core Minimal API, `net10.0`)
- Endpoints:
  - `GET /api/studies` — paginated list with filters
  - `GET /api/studies/{nctId}` — single study detail
  - `GET /api/pipeline-runs` — history
  - `GET /api/stats` — aggregate counts
- No CORS needed (Frontend talks directly, clients use nginx reverse proxy)
- Runs on port 5003 (5000 is used by macOS AirPlay Receiver / Control Center)

---

### Issue #14 — PR 5: Frontend project (DONE)

**Branch:** `feature/frontend`
**PR:** #21

**Goal:** Blazor Server UI for browsing studies.

**Changes:**
- New `Frontend/` project (Blazor Server, `net10.0`)
- Three pages:
  - **Search** — full-text search across studies, hover for abstract
  - **Study Detail** — single study view with investigators, PubMed papers, keywords
  - **Pipeline History** — list of past pipeline runs
- HTTP client talks to `DataApi` on port 5003
- Runs on port 5001

---

### Issue #15 — PR 6: Frontend tests (DONE)

**Branch:** `feature/frontend-tests`
**PR:** #22 (in review)

**Goal:** Coverage for the three Blazor pages.

**Changes:**
- New `Frontend.Tests/` project (MSTest + bUnit)
- 4 tests covering Search (2), Pipeline History (1), Study Detail (1) pages
- Uses `RichardSzalay.MockHttp` for API mocking
- Uses bUnit 2.0 `Render` API (`new BunitContext()` + `ctx.Render<T>()`)

---

### Issue #16 — PR 7: ARCHITECTURE.md + final polish (IN REVIEW)

**Branch:** `feature/architecture-docs`
**PR:** (current)

**Goal:** Documentation and deployment readiness.

**Changes:**
- `ARCHITECTURE.md` describing two-DO-droplet deployment
- `README.md` update with local dev instructions, new port (5003), project table
- `ROADMAP.md` — mark all issues done
- DataApi port changed from 5000→5003 (macOS port conflict)
- Frontend: added `app.UseAntiforgery()` (required by .NET 10)
- All projects build with 0 warnings/errors; all 25 tests pass

---

## Development Flow

1. **All work on feature branches** from `main`
2. **No merging until local verification**: `dotnet build` (0 errors/warnings) + `dotnet test` (all pass)
3. **Review existing plans**: Check `.opencode/plans/` for any relevant context before starting
4. **Each PR closes its issue**

## Local Quickstart

```bash
# Set connection string
export POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Database=clinical_trial_data;Username=$(whoami)"

# Full pipeline
dotnet run --project DataAggregators/ -- --count 50

# API + Frontend (separate terminals)
dotnet run --project DataApi/ --urls "http://localhost:5000"
dotnet run --project Frontend/ --urls "http://localhost:5001"
```