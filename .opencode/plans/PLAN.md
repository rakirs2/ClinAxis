# ClinicalTrialData — Restoration & Deployment Plan

## Decision Log

| Decision | Choice | Rationale |
|----------|--------|-----------|
| EF Core version | **Upgrade to 10.x** | Match net10.0 target framework; avoid compat shim risks |
| App deployment | **`dotnet publish` + systemd + Kestrel direct** | No nginx; .NET serves HTTPS directly via built-in HTTPS |
| Postgres local dev | **Docker container** | Single `docker run` or `docker compose up -d postgres` |
| Deploy target | **DO Droplet + systemd** | Closest to Microsoft docs; no container registry needed |
| IngestionApp | **Keep as console app** | Rename to `DataGenService` eventually, noted in roadmap |
| Blazor interactivity | **Interactive Server** | Uses SignalR natively; standard MS pattern for .NET 10 |
| DB test infra | **Testcontainers.PostgreSql** | Spins up disposable Docker Postgres containers programmatically. Works identically in Rider, CLI, and CI. |
| Test utilities location | **Single copy in `Scrapers/Testing/`** | Eliminates current 2x duplication, shared via `InternalsVisibleTo` |

---

## Core Principles (for all agents)

These are non-negotiable. Read this before any contribution.

### Workflow
1. **1 feature, 1 branch, 1 PR** — every new feature gets its own branch from `origin/main`. No bundled changes.
2. **No push to `main` without all tests passing** — CI blocks any merge where unit, integration, or live HTTP tests fail. `dotnet build` (0 errors/warnings) + `dotnet test` (all pass) are the minimum gates.
3. **Document as you go** — update this file with what was tried, what worked, what didn't. Never retry the same approach twice.
4. **It's OK to delete bad code** — refactor first, add features second.

### CI Integration Testing Philosophy
Integration tests are the backbone of the CI pipeline — they prove all components work together.

- **Run every integration test in CI.** No `[Ignore]`, no env var gates. If a test needs Postgres, Testcontainers or the CI service container handles it. If a test needs an external API, it calls the real endpoint.
- **Testcontainers for database management.** `DbTestBase` uses `Testcontainers.PostgreSql` to spin up a disposable PostgreSQL container per test class. No manual Docker steps. Works identically locally and in CI.
- **Transaction rollback for isolation.** Each test wraps DB operations in a transaction. `[TestCleanup]` rolls back — zero data leakage between tests. No CREATE/DROP DATABASE per test.
- **Avoid mocks. Prefer pre-seeded data.** Use `SnapshotDb` with known golden data in a real PostgreSQL database. Only use fake HTTP handlers when testing an HTTP client against an external API that would rate-limit in CI.
- **Schema guards catch silent breakage.** Every external API response gets a `JsonDocument` schema guard test that fails if required fields are removed or renamed.
- **Full integration = start services → run tests → tear down.** CI starts DataApi + Frontend as background processes, waits for readiness, runs the `HttpLive` test suite against real services talking to real Postgres. Validates the full chain: Frontend → API → DB.

**Goal:** Every PR proves the system works end-to-end before it merges. No "works on my machine."

### Tech Stack

| Layer | Technology | Why |
|-------|-----------|-----|
| Language | C# .NET 10 (`net10.0`) | Latest stable |
| Frontend | Blazor Server with `AddInteractiveServerComponents()` | SignalR built-in; Microsoft's standard |
| ORM | EF Core 10.x + Npgsql 9.x | Must match target framework |
| Database | PostgreSQL 15+ (via Docker for local dev) | Standard relational DB |
| Testing | MSTest only (`MSTest.TestAdapter` + `MSTest.TestFramework`) | No xUnit, no NUnit |
| CI/CD | GitHub Actions only | Source of truth for builds |
| Deploy | DigitalOcean Droplet — `dotnet publish` → SCP → systemd → Kestrel directly on port 80/443 | Per [Microsoft docs](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx) and [DigitalOcean docs](https://docs.digitalocean.com/developer-center/deploying-to-digitalocean-with-github-actions/) |
| Containers | Testcontainers.PostgreSql for test DB | Disposable Docker containers managed by the test runner |

### Three DB Testing Modes
All tests use a **single fixed PostgreSQL database** (`clinical_trial_data_test`) in a Testcontainers-managed Docker container. No `CREATE DATABASE`/`DROP DATABASE` per test class. Isolation is via **transaction rollback**.

| Mode | Class | Use Case |
|------|-------|----------|
| **Integration/IO** | `DbTestBase` | Testcontainers spins up Postgres container, runs migrations once per class, txn rollback per method. Fast and isolated. |
| **Snapshot** | `SnapshotDb` | Same container, seeded with known golden data. Deterministic assertions against a fixed dataset. |
| **Persistent/fiddle** | `SnapshotDb(persist: true)` | Same as snapshot but no rollback — DB stays for manual inspection via any SQL tool. |

### Running Tests from Rider
1. Ensure Docker Desktop is running (no manual `docker compose` needed — Testcontainers manages containers)
2. Click **Run All Tests** in the test runner — every test, including integration tests against a real Postgres via Testcontainers, executes locally. No exceptions. No manual setup.
3. Set `POSTGRES_CONNECTION_STRING` env var in Rider if you need to override defaults (usually not needed with Testcontainers).

### Deployment Standard
- `dotnet publish --self-contained -r linux-x64`
- SCP publish output to Droplet
- systemd unit files for process management
- Kestrel serves HTTPS directly on port 80/443. TLS via .NET's built-in HTTPS + Let's Encrypt cert.
- No Docker for .NET apps in production. Docker is for local/test PostgreSQL only.
- No nginx. Keep the stack minimal.

### When in Doubt, Default to User Choice
- If there is no clear default documented here, **present options to the user and let them decide**. Don't guess and don't default to a personal preference.
- If standard docs answer the question, reference them (e.g., [MS Learn](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx), [DO guides](https://docs.digitalocean.com/developer-center/deploying-to-digitalocean-with-github-actions/)).
- If standard docs don't give a clear default, list the plausible approaches with trade-offs and ask.

---

## Current State Analysis

### What Works (in code, not necessarily verified locally)
- 7 projects targeting `net10.0` in a single solution
- EF Core 8.0.4 with PostgreSQL 15+, Npgsql 8.0.4 — 10 DbSets, 6 migrations
- REST API (DataApi) on port 5003 — 6 endpoints
- Blazor Server frontend on port 5001 — 4 pages
- Scraper pipeline (ClinicalTrials.gov v2 + PubMed EFetch) — console-driven via IngestionApp
- Tests — MSTest unit tests (fake HTTP), integration tests (CLI-based ephemeral Postgres), bUnit tests, live HTTP tests
- CI — GitHub Actions with Postgres service container, two-stage test job
- Strict code style — `.editorconfig` + `Directory.Build.props` (warnings as errors)

### Issues Found

| # | Issue | Impact | Fixed In |
|---|-------|--------|----------|
| 1 | EF Core 8.0.4 on `net10.0` — version mismatch | Must upgrade to EF Core 10.x | PR 1 |
| 2 | No `appsettings.json` in any project | Hard to run without env vars set | PR 1 |
| 3 | No `docker-compose.yml` for Postgres | Every new dev must install/configure Postgres manually | PR 1 |
| 4 | `EphemeralPostgresDatabase` uses CLI `createdb`/`dropdb` | Requires Homebrew Postgres on PATH, doesn't work with Docker-only setup | PR 1 |
| 5 | Test utilities duplicated 2x (4 files total) | Two identical copies in Scrapers.Tests and Scrapers.IntegrationTests | PR 1 |
| 6 | Home.razor uses vanilla JS (`site.js`) for search | Ignores Blazor's component model | PR 2 |
| 7 | No SignalR — static SSR only | No real-time capability | PR 2 |
| 8 | No Investigators page | Required for Feature 2 | PR 3 |
| 9 | Search is minimal — single text input | Required for Feature 3 | PR 4 |
| 10 | Status page calls DataApi directly | Tight coupling, no real-time | PR 5 |
| 11 | No GitHub Actions deploy workflow | Cannot deploy from CI | PR 6 |
| 12 | Stale docs (ROADMAP, README) | Reference non-existent projects | PR 0 |

---

## Detailed Test Infrastructure Design

### Current (broken)

```
Scrapers.Tests/Utilities/EphemeralPostgresDatabase.cs   ← CLI createdb/dropdb
Scrapers.Tests/Utilities/EphemeralDbTestBase.cs
Scrapers.IntegrationTests/Utilities/EphemeralPostgresDatabase.cs  ← DUPLICATE
Scrapers.IntegrationTests/Utilities/EphemeralDbTestBase.cs         ← DUPLICATE
Scrapers.IntegrationTests/Utilities/TestBase.cs    ← Unused
```

**Problems:**
- `createdb`/`dropdb` CLI requires `libpq` on the PATH — doesn't work with Docker-only setup
- Duplicated 2x across test projects
- CREATE/DROP DATABASE per test class is slow and needs elevated DB permissions

### Target

```
Scrapers/Testing/
  DbTestBase.cs              ← Single copy, uses Testcontainers.PostgreSql
  SnapshotDb.cs              ← Single copy, seeded golden data + persist mode
  SeedData.cs                ← Single copy, known test data (C# objects)
```

Shared via `InternalsVisibleTo` in `Scrapers.csproj`. Testcontainers.PostgreSql NuGet package added to `Scrapers.csproj`.

#### `DbTestBase` (Testcontainers-based)

```csharp
public abstract class DbTestBase
{
    private static PostgreSqlContainer? _container;
    private static string _connectionString = "";
    private static bool _schemaInitialized;
    private static readonly Lock SchemaLock = new();
    protected string ConnectionString => _connectionString;
    protected ClinicalTrialsContext Context { get; private set; } = null!;
    private IDbContextTransaction? _txn;

    [ClassInitialize]
    public static async Task InitClass(TestContext _)
    {
        _container = new PostgreSqlBuilder()
            .WithDatabase("clinical_trial_data_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        var opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .UseNpgsql(_connectionString).Options;
        await using var ctx = new ClinicalTrialsContext(opts);
        await ctx.Database.MigrateAsync();
        _schemaInitialized = true;
    }

    [ClassCleanup]
    public static async Task CleanupClass()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }

    [TestInitialize]
    public async Task Init()
    {
        var opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .UseNpgsql(_connectionString).Options;
        Context = new ClinicalTrialsContext(opts);
        _txn = await Context.Database.BeginTransactionAsync();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_txn != null) { await _txn.RollbackAsync(); await _txn.DisposeAsync(); }
        if (Context != null) { await Context.DisposeAsync(); }
    }
}
```

**How tests execute:**
1. `[ClassInitialize]` — Testcontainers spins up a PostgreSQL Docker container. Runs migrations once.
2. `[TestInitialize]` — Each test method gets a new context + transaction.
3. Test runs — reads/writes against the real Postgres.
4. `[TestCleanup]` — Transaction rolls back. No test sees another's data.
5. `[ClassCleanup]` — Testcontainers stops and removes the container.

#### `SnapshotDb` (seeded golden data + persist mode)

```csharp
public sealed class SnapshotDb : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container;
    private readonly bool _persist;
    public string ConnectionString { get; }

    public SnapshotDb(bool persist = false)
    {
        _persist = persist;
        _container = new PostgreSqlBuilder()
            .WithDatabase(persist ? "clinical_trial_data_snapshot" : "ct_snapshot_" + Guid.NewGuid().ToString("N").ToLowerInvariant())
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        _container.StartAsync().GetAwaiter().GetResult();
        ConnectionString = _container.GetConnectionString();

        // Run migrations + seed
        var opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .UseNpgsql(ConnectionString).Options;
        using var ctx = new ClinicalTrialsContext(opts);
        ctx.Database.Migrate();
        SeedData.SeedAsync(ctx).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_persist)
            await _container.DisposeAsync();
        // If persist=true, container + DB stay for manual inspection
    }
}
```

#### `SeedData` (known test data, typed C#)

```csharp
internal static class SeedData
{
    internal static readonly StudyEntity Study1 = new()
    {
        NctId = "NCT00000001",
        BriefTitle = "Test Study Alpha",
        OverallStatus = "ACTIVE",
        // ... full known data
    };

    internal static async Task SeedAsync(ClinicalTrialsContext ctx)
    {
        ctx.Studies.AddRange(Study1, Study2, Study3);
        ctx.Investigators.AddRange(Investigator1, Investigator2);
        await ctx.SaveChangesAsync();
    }
}
```

---

## Step-by-Step PR Plan

### PR 0: Agent Guidelines & Documentation (CURRENT — open PR #32)
**Goal:** Update all documentation to reflect current decisions and conventions.

**No code changes.** Files modified: `AGENTS.md`, `ROADMAP.md`, `README.md`, `ARCHITECTURE.md`, `.opencode/plans/PLAN.md`.

**Verification:** `dotnet build` (0 errors, 0 warnings).

---

### PR 1: Build Fix + Package Upgrades + Testcontainers Infrastructure
**Goal:** `dotnet build` + `dotnet test` passing. Testcontainers working in Rider, CLI, and CI.

**Steps:**
1. Add `Testcontainers.PostgreSql` NuGet package to `Scrapers.csproj`
2. Create `Scrapers/Testing/` with:
   - `DbTestBase.cs` — Testcontainers-based, txn rollback per method
   - `SnapshotDb.cs` — seeded golden data + persist mode
   - `SeedData.cs` — known test data entities
3. Add `InternalsVisibleTo` for both test assemblies in `Scrapers.csproj`
4. **Delete** the 4 duplicated files:
   - `Scrapers.Tests/Utilities/EphemeralPostgresDatabase.cs`
   - `Scrapers.Tests/Utilities/EphemeralDbTestBase.cs`
   - `Scrapers.IntegrationTests/Utilities/EphemeralPostgresDatabase.cs`
   - `Scrapers.IntegrationTests/Utilities/EphemeralDbTestBase.cs`
   - `Scrapers.IntegrationTests/Utilities/TestBase.cs`
5. Upgrade EF Core 8.0.4 → 10.x, Npgsql 8.0.4 → 9.x, Caching 8.0.1 → 10.x across all `.csproj` files
6. Fix any breaking changes in EF Core 10.x API surface
7. Update all test classes that inherit from old `EphemeralDbTestBase` → new `DbTestBase`
8. Add `DataApi/appsettings.json` with connection string default
9. Add `Frontend/appsettings.json` with DataApi URL default
10. Add `docker-compose.yml` (Postgres only — for non-test manual use)
11. Fix stale docs (`README.md`, `ROADMAP.md`)

**Verification:** `dotnet build` (0 errors, 0 warnings), `dotnet test` (all pass — Testcontainers manages Postgres automatically)

---

### PR 2: Feature 1 — Display Studies from Database
**Goal:** Home page shows paginated studies from DB. First working UI feature.

**Steps:**
1. Switch Frontend to Interactive Server:
   - `builder.Services.AddRazorComponents().AddInteractiveServerComponents()`
   - `<Routes @rendermode="InteractiveServer" />` in `App.razor`
   - Add `@rendermode InteractiveServer` to pages
2. Rewrite `Home.razor` as proper Blazor component:
   - `OnInitializedAsync` fetches from DataApi via HttpClient
   - Paginated table: NCT ID, Title, Status, Conditions, Phases
   - Search text box (Blazor-bound, not vanilla JS)
   - Page navigation (Prev/Next)
3. Remove `wwwroot/js/site.js` and `/api-proxy/studies` proxy endpoint
4. Update Frontend `appsettings.json` for DataApi base URL
5. Update Frontend.Tests for interactive rendering
6. Add snapshot tests using `SnapshotDb` for the study listing logic

**Verification:** Navigate to `/` → studies with pagination. Search returns filtered results. `dotnet test` passes.

---

### PR 3: Feature 2 — Principal Investigators Page
**Goal:** New `/investigators` page listing all PIs.

**Steps:**
1. Add `GET /api/investigators` to DataApi (paginated, searchable by name)
2. Add repository method for investigator list with study counts
3. New `Investigators.razor` page:
   - Table: Name, Affiliation, Study Count, PubMed Paper Count
   - Click PI → navigate to studies filtered by that PI
4. Add nav link in `MainLayout.razor`
5. Snapshot tests for investigator listing and search

**Verification:** `/investigators` lists PIs with counts. Search filters work. `dotnet test` passes.

---

### PR 4: Feature 3 — Advanced Search Page
**Goal:** Search with all filter criteria.

**Steps:**
1. Update `GET /api/studies` with params: `condition`, `keyword`, `enrollmentMin`, `enrollmentMax`, `dateFrom`, `dateTo`
2. Update repository's `GetStudiesPagedAsync` / `CountStudiesFilteredAsync`
3. Create `Search.razor` with:
   - Text search, Status multi-select, Phase multi-select
   - Condition / Keyword text fields, Enrollment range, Date range
   - Paginated results
4. Snapshot tests for every filter combination

**Verification:** All filter combinations return correct results. `dotnet test` passes.

---

### PR 5: Feature 4 — Status Page with Real-Time Updates
**Goal:** `/status` shows live DB & scraper stats via Blazor's SignalR circuit.

**Steps:**
1. Enhance `GET /api/telemetry` with pipeline runs, scrape events, row counts, health indicators
2. Convert `Status.razor` to Interactive Server:
   - Timer-driven auto-refresh (10–15s interval via `InvokeAsync` on the existing Blazor SignalR circuit)
   - DB stats panel, Pipeline timeline, Scrape events log, Aggregation summary
3. Snapshot tests for telemetry endpoint and status page rendering

**Verification:** `/status` auto-updates without page refresh. Shows live pipeline data. `dotnet test` passes.

---

### PR 6: GitHub Actions Deploy → DigitalOcean Droplet
**Goal:** Push to `main` → build → test → deploy → verify → promote or rollback.

**Steps:**
1. Add `deploy/` directory with:
   - `systemd/ct-data-api.service`
   - `systemd/ct-frontend.service`
   - `setup.sh` (one-time server provisioning script)
2. Create `.github/workflows/deploy.yml` with three phases:

**Phase 1 — Test:**
```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - dotnet build
      - dotnet test                          # All tests (Testcontainers uses Docker-in-Docker or service container)
      - # Start services for HttpLive tests
        dotnet run --project DataApi/ --no-build &
        dotnet run --project Frontend/ --no-build &
        dotnet test --filter "TestCategory=HttpLive"
```

**Phase 2 — Deploy:**
```yaml
  deploy:
    needs: test
    steps:
      - dotnet publish DataApi/ -c Release -r linux-x64 --self-contained -o publish/data-api
      - dotnet publish Frontend/ -c Release -r linux-x64 --self-contained -o publish/frontend
      - ssh: mv /var/www/ct-data/current /var/www/ct-data/previous   # Save rollback target
      - scp publish/* to droplet:/var/www/ct-data/current/
      - ssh: systemctl restart ct-data-api ct-frontend
```

**Phase 3 — Verify (promote or rollback):**
```yaml
  verify:
    needs: deploy
    steps:
      - curl -f --retry 5 -o /dev/null -w "%{http_code}" https://<droplet>/   # Expect 200 (Frontend loads, Blazor Server serves interactivity)
      # On success: promote
      - ssh: rm -rf /var/www/ct-data/previous
      # On failure: rollback
      - ssh: |
          rm -rf /var/www/ct-data/current
          mv /var/www/ct-data/previous /var/www/ct-data/current
          systemctl restart ct-data-api ct-frontend
```

**Verification:** `git push main` → GH Actions green → site live at droplet. `dotnet test` blocks if any test fails.

---

## Verification Checklist (per PR)

```
□ dotnet build (0 errors, 0 warnings)
□ dotnet test (all pass) — Testcontainers handles DB automatically
□ Rider test runner green (all tests including integration)
□ Manual smoke test of changed pages
```

---

## Quick Reference: Running Tests

| Scenario | Command |
|----------|---------|
| All tests | `dotnet test` |
| Unit only | `dotnet test Scrapers.Tests/` |
| Integration only | `dotnet test Scrapers.IntegrationTests/ --filter "TestCategory!=HttpLive"` |
| Live HTTP only | Start services manually first, then `dotnet test --filter "TestCategory=HttpLive"` |
| Snapshot tests | `dotnet test --filter "FullyQualifiedName~Snapshot"` |
| Persistent/snapshot fiddle | `CT_PERSIST_SNAPSHOT=true dotnet test --filter "..."` then connect with `psql` or Rider |

Testcontainers manages PostgreSQL automatically — no manual `docker compose` needed for tests. Docker Desktop just needs to be running.

---

*This document is updated as execution progresses. Failed approaches are noted alongside with rationale.*
