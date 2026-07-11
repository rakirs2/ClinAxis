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
| DB test infra | **Npgsql-based ephemeral, not CLI** | Removes Homebrew/libpq dependency, works with Docker-only setup |
| Test utilities location | **Single copy in `Scrapers/Testing/`** | Eliminates current 2x duplication, shared via `InternalsVisibleTo` |

## Core Principles (for all agents)

These are the non-negotiable priorities for this project. Read this before any contribution.

### Workflow
1. **1 feature, 1 branch, 1 PR** — every new feature gets its own branch from `origin/main`. No bundled changes.
2. **Local verification required** — never merge a change that hasn't passed `dotnet build` (0 errors/warnings) + `dotnet test` (all pass) + `docker compose up -d postgres && dotnet test` (full suite).
3. **Document as you go** — update `.opencode/plans/PLAN.md` with what was tried, what worked, what didn't. Never retry the same approach twice.
4. **It's OK to delete bad code** — refactor first, add features second.

### CI Integration Testing Philosophy
Integration tests are the backbone of our CI pipeline. They prove that all components actually work together. Here's the approach:

- **Run every integration test in CI.** Do not gate tests behind `[Ignore]`, environment variables, or manual setup steps. If a test interacts with PostgreSQL, the CI Postgres service container handles it. If a test needs external API access, it calls the real API.
- **Ephemeral databases give us isolation.** Each test class gets its own temp database (created via Npgsql, dropped on cleanup). Tests never collide, even running in parallel.
- **Real HTTP calls only when needed.** External APIs (ClinicalTrials.gov, PubMed) are tested via live smoke tests in separate test files. Prefer `SnapshotDb` with captured real data for most assertions. This avoids CI failures from upstream API changes while still catching breakage.
- **Schema guards catch silent breakage.** Every external API response gets a `JsonDocument` schema guard test that fails if required fields are removed or renamed. This runs on every push.
- **Full integration = start services, run tests, tear down.** In CI, we start DataApi + Frontend as background processes, wait for readiness, then run the `HttpLive` test suite against the real services talking to a real Postgres. This validates the full chain: Frontend → API → DB.
- **Avoid mocks at every level.** Even unit tests should prefer `SnapshotDb` with pre-seeded golden data. The only exception is fake HTTP handlers for external APIs that would rate-limit or fail in CI (e.g., ClinicalTrials.gov, PubMed). Pre-seeded data is compile-time checked, refactorable, and more trustworthy than mock setups.

**Goal:** Every PR proves the system works end-to-end before it merges. No "works on my machine."

### Tech Stack
- **Language**: C# .NET 10 (`net10.0`)
- **Frontend**: Blazor Server with `AddInteractiveServerComponents()` (brings SignalR automatically)
- **ORM**: Entity Framework Core 10.x + Npgsql 9.x + PostgreSQL 15+
- **Testing**: MSTest only (no xUnit, no NUnit)
- **CI/CD**: GitHub Actions only
- **Deploy**: DigitalOcean Droplet, `dotnet publish` → SCP → systemd, Kestrel directly on 80/443

### Three DB Testing Modes
1. **Integration/IO tests** — ephemeral database per test class, fast, isolated
2. **Snapshot tests** — pre-seeded database with known data for deterministic assertions
3. **Local fiddle** — persistent seeded database for manual inspection via any SQL tool

### When in Doubt
- Follow Microsoft Learn docs first (e.g., [Host ASP.NET Core on Linux with Nginx](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx))
- Follow DigitalOcean community tutorials second
- Default choices are better than custom ones

---

## Current State Analysis

### What Works (in code, not necessarily verified locally)
- 7 projects targeting `net10.0` in a single solution
- EF Core 8.0.4 with PostgreSQL 15+, Npgsql 8.0.4 — 10 DbSets, 6 migrations
- REST API (DataApi) on port 5003 — 6 endpoints
- Blazor Server frontend on port 5001 — 4 pages
- Scraper pipeline (ClinicalTrials.gov v2 + PubMed EFetch) — console-driven via IngestionApp
- Tests — MSTest unit tests (fake HTTP), integration tests (ephemeral Postgres), bUnit tests, live HTTP tests
- CI — GitHub Actions with Postgres service container, two-stage test job
- Strict code style — `.editorconfig` + `Directory.Build.props` (warnings as errors)

### Issues Found

| # | Issue | Impact | Fixed In |
|---|-------|--------|----------|
| 1 | EF Core 8.0.4 on `net10.0` — version mismatch | Must upgrade to EF Core 10.x | PR 0 |
| 2 | No `appsettings.json` in any project | Hard to run without env vars set | PR 0 |
| 3 | No `docker-compose.yml` for Postgres | Every new dev must install/configure Postgres manually | PR 0 |
| 4 | `EphemeralPostgresDatabase` uses CLI `createdb`/`dropdb` | Requires Homebrew Postgres on PATH, doesn't work with Docker-only setup | PR 0 |
| 5 | Test utilities duplicated 2x (4 files total) | Two identical copies in Scrapers.Tests and Scrapers.IntegrationTests | PR 0 |
| 6 | Home.razor uses vanilla JS (`site.js`) for search | Ignores Blazor's component model | PR 1 |
| 7 | No SignalR — static SSR only | No real-time capability | PR 1 |
| 8 | No Investigators page | Required for Feature 2 | PR 2 |
| 9 | Search is minimal — single text input | Required for Feature 3 | PR 3 |
| 10 | Status page calls DataApi directly | Tight coupling, no real-time | PR 4 |
| 11 | No GitHub Actions deploy workflow | Cannot deploy from CI | PR 5 |
| 12 | Stale docs (ROADMAP, README) | Reference non-existent projects | PR 0 |

---

## Detailed Testing Infrastructure Design

### Current (broken) — CLI-based ephemeral DBs

```
Scrapers.Tests/Utilities/EphemeralPostgresDatabase.cs   ← CLI createdb/dropdb
Scrapers.Tests/Utilities/EphemeralDbTestBase.cs
Scrapers.IntegrationTests/Utilities/EphemeralPostgresDatabase.cs  ← DUPLICATE
Scrapers.IntegrationTests/Utilities/EphemeralDbTestBase.cs         ← DUPLICATE
Scrapers.IntegrationTests/Utilities/TestBase.cs    ← Unused
```

**Problem:** `createdb` requires `libpq` CLI to be on the PATH. Docker Postgres doesn't provide this to the host. So tests fail when Postgres is in Docker.

### Proposed — Npgsql-based, single source of truth

```
Scrapers/Testing/
  EphemeralPostgresDatabase.cs     ← Single copy, no duplication
  EphemeralDbTestBase.cs           ← Single copy, no duplication
  SnapshotDb.cs                    ← NEW: seeded + optional persist
  SeedData.cs                      ← NEW: known test data
```

**How it works:**

#### `EphemeralPostgresDatabase` (Npgsql-based, no CLI)

```csharp
public sealed class EphemeralPostgresDatabase : IAsyncDisposable
{
    public string DatabaseName { get; }
    public string ConnectionString { get; }

    public EphemeralPostgresDatabase()
    {
        var baseConn = ConnectionStringProvider.DefaultNoPooling;
        var (host, port, username, password) = ParseConn(baseConn);
        DatabaseName = "ct_test_" + Guid.NewGuid().ToString("N").ToLowerInvariant();
        ConnectionString = $"Host={host};Port={port};Database={DatabaseName};Username={username};Pooling=false;{(password != "" ? $"Password={password};" : "")}";

        // Connect to 'postgres' template database to create/find new DB
        using var conn = new NpgsqlConnection(
            $"Host={host};Port={port};Database=postgres;Username={username};{(password != "" ? $"Password={password};" : "")}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{DatabaseName}\"";
        cmd.ExecuteNonQuery();
    }

    public async ValueTask DisposeAsync()
    {
        var (host, port, username, password) = ParseConn(ConnectionStringProvider.DefaultNoPooling);
        using var conn = new NpgsqlConnection(
            $"Host={host};Port={port};Database=postgres;Username={username};{(password != "" ? $"Password={password};" : "")}");
        await conn.OpenAsync();

        // Kill all connections to the test DB
        using var term = new NpgsqlCommand(@"
            SELECT pg_terminate_backend(pg_stat_activity.pid)
            FROM pg_stat_activity
            WHERE datname = @db AND pid <> pg_backend_pid()", conn);
        term.Parameters.AddWithValue("db", DatabaseName);
        await term.ExecuteNonQueryAsync();

        // Drop the database
        using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{DatabaseName}\"", conn);
        await drop.ExecuteNonQueryAsync();
    }
}
```

**Benefits:**
- Zero CLI dependencies — works with Docker Postgres, native Postgres, any Postgres
- `NpgsqlConnection` is already a project dependency (via EF Core), no new NuGet packages
- Same API surface as current `EphemeralPostgresDatabase` — minimal test code changes

#### `EphemeralDbTestBase` (de-duplicated)

```csharp
// In Scrapers/Testing/ — single copy, shared via InternalsVisibleTo
public abstract class EphemeralDbTestBase
{
    protected EphemeralPostgresDatabase EphemeralDb = null!;
    protected string ConnectionString = null!;
    protected ClinicalTrialsContext Context = null!;
    private IDbContextTransaction? _txn;

    [TestInitialize]
    public async Task Init()
    {
        EphemeralDb = new EphemeralPostgresDatabase();
        ConnectionString = EphemeralDb.ConnectionString;
        var opts = new DbContextOptionsBuilder<ClinicalTrialsContext>()
            .UseNpgsql(ConnectionString).Options;
        Context = new ClinicalTrialsContext(opts);
        await Context.Database.MigrateAsync();
        _txn = await Context.Database.BeginTransactionAsync();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_txn != null) { await _txn.RollbackAsync(); await _txn.DisposeAsync(); }
        if (Context != null) { await Context.DisposeAsync(); }
        if (EphemeralDb != null) { await EphemeralDb.DisposeAsync(); }
    }
}
```

#### `SnapshotDb` (NEW — for deterministic/golden data tests)

```csharp
public sealed class SnapshotDb : IAsyncDisposable
{
    public string ConnectionString { get; }
    private readonly string _dbName;
    private readonly bool _persist;

    /// <param name="persist">If true, database is NOT dropped on dispose (for local fiddling)</param>
    public SnapshotDb(bool persist = false)
    {
        _persist = persist;
        _dbName = persist ? "clinical_trial_data_snapshot" : "ct_snapshot_" + Guid.NewGuid().ToString("N").ToLowerInvariant();
        // Create via Npgsql (same pattern as EphemeralPostgresDatabase)
        ConnectionString = BuildConnString(_dbName);
        CreateDatabase();
        SeedAsync().GetAwaiter().GetResult();  // Populate with known data
    }

    public async ValueTask DisposeAsync()
    {
        if (!_persist) { await DropDatabaseAsync(); }
        // If persist=true, DB stays for manual inspection
    }
}
```

**Usage in tests:**

```csharp
[TestMethod]
public async Task CountByStatus_ReturnsCorrectCount()
{
    // Creates temp DB, seeds with known data, runs test, drops DB
    await using var snap = new SnapshotDb();

    var repo = new StudyRepository(snap.ConnectionString);
    var count = await repo.CountStudiesFilteredAsync(status: "ACTIVE");

    Assert.AreEqual(3, count);  // Known from seed data
}
```

**Usage for local fiddling:**

```bash
# Set this env var to keep the DB around
export CT_PERSIST_SNAPSHOT=true
dotnet test --filter "FullyQualifiedName~SnapshotTests"
# Now connect to clinical_trial_data_snapshot with any SQL tool
psql clinical_trial_data_snapshot
# Or via Rider Database tool window
```

Or, a specific "seed only" mode:
```csharp
// A standalone script/method in the test project:
var snap = new SnapshotDb(persist: true);
Console.WriteLine($"Snapshot DB ready at: {snap.ConnectionString}");
Console.WriteLine("Press Enter to drop it...");
Console.ReadLine();
await snap.DisposeAsync();
```

#### `SeedData` (known test data)

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

This replaces the need for JSON fixture files for DB tests. The data is in code — it's compiled, type-safe, and refactorable.

---

## PR Breakdown

### PR 0: Fix Build, Upgrade EF Core, Fix Test Infrastructure
**Goal:** `dotnet build` + `dotnet test` passing. Tests work with Docker-only Postgres. One single PR because all the infra changes are foundational.

**Package upgrades:**
- `Microsoft.EntityFrameworkCore` 8.0.4 → 10.0.x
- `Microsoft.EntityFrameworkCore.Design` 8.0.4 → 10.0.x
- `Npgsql` 8.0.4 → 9.0.x
- `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.4 → 9.0.x
- `Microsoft.Extensions.Caching.Memory` 8.0.1 → 10.0.x

**Test infrastructure (de-duplicate + fix):**
- Create `Scrapers/Testing/` folder with:
  - `EphemeralPostgresDatabase.cs` — Npgsql-based, no CLI (single source)
  - `EphemeralDbTestBase.cs` — single source of truth
  - `SnapshotDb.cs` — seeded DB for deterministic tests + persist mode
  - `SeedData.cs` — known test data
- Add `InternalsVisibleTo` to `Scrapers.csproj` for both test assemblies
- **Delete** the 4 duplicated files:
  - `Scrapers.Tests/Utilities/EphemeralPostgresDatabase.cs`
  - `Scrapers.Tests/Utilities/EphemeralDbTestBase.cs`
  - `Scrapers.IntegrationTests/Utilities/EphemeralPostgresDatabase.cs`
  - `Scrapers.IntegrationTests/Utilities/EphemeralDbTestBase.cs`
  - `Scrapers.IntegrationTests/Utilities/TestBase.cs`
- Update references in all test classes to point to `Scrapers.Testing`

**Config & docs:**
- Add `DataApi/appsettings.json` with connection string default
- Add `Frontend/appsettings.json` with DataApi URL default
- Add `docker-compose.yml` (Postgres only — one service)
- Fix stale README.md references to `DataAggregators/`
- Update ROADMAP.md to match reality

**Verification:** `dotnet build` (0 errors, 0 warnings), `dotnet test` (all pass), `docker compose up -d postgres && dotnet test` (all pass)

---

### PR 1: Feature 1 — Display Studies from Database
**Goal:** Home page shows paginated studies from DB.

- Switch Frontend to Interactive Server (enables SignalR):
  - `builder.Services.AddRazorComponents().AddInteractiveServerComponents()`
  - `<Routes @rendermode="InteractiveServer" />` in `App.razor`
- Rewrite `Home.razor` as proper Blazor component:
  - `OnInitializedAsync` fetches studies via HttpClient → DataApi at `http://localhost:5003`
  - Paginated table: NCT ID, Title, Status, Conditions, Phases
  - Search text box (Blazor-bound, not vanilla JS)
  - Page navigation (Prev/Next)
- Remove `wwwroot/js/site.js` and `/api-proxy/studies` endpoint
- Update Frontend `appsettings.json` to configure DataApi base URL
- Update Frontend.Tests for interactive rendering

**Verification:** Navigate to `/` → see studies with pagination. Search works. `dotnet test` passes.

---

### PR 2: Feature 2 — Principal Investigators Page
**Goal:** New `/investigators` page listing all PIs.

- Add `GET /api/investigators` to DataApi (paginated, searchable)
- Add repository method `GetInvestigatorsPagedAsync` with study counts
- New `Investigators.razor` page:
  - Table: Name, Affiliation, Study Count, PubMed Paper Count
  - Click PI → search studies by that PI
- Add nav link in `MainLayout.razor`
- Frontend.Tests for Investigators page

**Verification:** `/investigators` lists all PIs with counts. `dotnet test` passes.

---

### PR 3: Feature 3 — Advanced Search Page
**Goal:** Search with all filter criteria.

- Update `GET /api/studies` to accept: `condition`, `keyword`, `enrollmentMin`, `enrollmentMax`, `dateFrom`, `dateTo`
- Update repository's `GetStudiesPagedAsync` / `CountStudiesFilteredAsync`
- Create `Search.razor` (new name for search-specific page):
  - Text search input
  - Status multi-select (Active, Completed, Suspended, Withdrawn, etc.)
  - Phase multi-select (Phase 1, Phase 2, Phase 3, Phase 4, N/A)
  - Condition / Keyword text fields
  - Enrollment range (min/max number inputs)
  - Date range pickers (start date, completion date)
  - Paginated results table
- Frontend.Tests for all filter combinations

**Verification:** All filter combos return correct results. `dotnet test` passes.

---

### PR 4: Feature 4 — Status Page with Real-Time Updates
**Goal:** `/status` shows live DB & scraper stats via Blazor's SignalR circuit.

- Enhance `GET /api/telemetry` with:
  - Full pipeline run history (last 10 runs)
  - Recent scrape events (last 50)
  - Database row counts (studies, investigators, pubmed, etc.)
  - Scraper last-run timestamps
  - Health indicators
- Convert `Status.razor` to Interactive Server:
  - Timer-driven auto-refresh every 10–15s (uses `InvokeAsync` on the existing Blazor SignalR circuit — no separate hub needed)
  - Database stats panel
  - Pipeline timeline with status badges
  - Scrape events log
  - Aggregation summary
- Frontend.Tests for Status page

**Verification:** `/status` auto-updates without page refresh. Shows live pipeline data. `dotnet test` passes.

---

### PR 5: GitHub Actions Deploy → DigitalOcean Droplet
**Goal:** Push to `main` → build → test → deploy → verify → promote or rollback.

**Deployment model (staged, ephemeral):**
- CI runs all tests first. If tests fail, deploy is blocked.
- Deploy to the droplet replaces the live services. No separate staging environment — the droplet itself is the staging target.
- Smoke tests run against the droplet. If they pass, the deploy is promoted (keeps running). If they fail, the previous version is restored.
- `dotnet publish --self-contained -r linux-x64 -o publish`
- SCP publish folders to droplet
- systemd services for DataApi and Frontend
- Kestrel serves HTTPS directly on port 80/443. TLS via .NET's built-in HTTPS + Let's Encrypt cert.
- No nginx. Keep the stack minimal.

**Files to add to repo:**
- `deploy/systemd/ct-data-api.service`
- `deploy/systemd/ct-frontend.service`
- `deploy/setup.sh` (one-time server setup script)
- `.github/workflows/deploy.yml`

**In this PR:**
- Update `ARCHITECTURE.md` to reflect native .NET + systemd
- Update `README.md` with deployment instructions
- Add systemd unit files under `deploy/`

**One-time server setup** (instructions in deploy/setup.sh):
- Install .NET 10 runtime
- Create `/var/www/clinical-trial-data/` and `/var/www/clinical-trial-data/previous/` directories
- Copy systemd unit files, enable + start services
- Configure Kestrel for HTTPS with a Let's Encrypt cert

**GitHub Actions `deploy.yml` — three-phase pipeline:**
```yaml
jobs:
  test:
    # Phase 1: Run ALL tests (unit + integration + live HTTP)
    services: { postgres: ... }
    steps:
      - dotnet build
      - dotnet test                    # Block deploy if any test fails
      - dotnet run --project DataApi/ &    # Start services for live tests
        dotnet run --project Frontend/ &
      - dotnet test --filter "TestCategory=HttpLive"

  deploy:
    needs: test                        # Only runs if tests pass
    runs-on: ubuntu-latest
    steps:
      - dotnet publish DataApi/ -c Release -r linux-x64 --self-contained -o publish/data-api
      - dotnet publish Frontend/ -c Release -r linux-x64 --self-contained -o publish/frontend
      - ssh: mv /var/www/clinical-trial-data/current /var/www/clinical-trial-data/previous
        # Save current version as rollback target
      - scp publish/* to droplet:/var/www/clinical-trial-data/current/
      - ssh: systemctl restart ct-data-api ct-frontend

  verify:
    needs: deploy                      # Only runs if deploy succeeds
    runs-on: ubuntu-latest
    steps:
      - curl -f --retry 5 https://<droplet-ip>/api/studies?page=1&pageSize=1
        # Expects HTTP 200 + valid JSON with "data" and "total" fields
      - curl -f https://<droplet-ip>/
        # Expects HTTP 200 (Frontend loads)
      - # On success: promote (delete the previous backup)
        ssh: rm -rf /var/www/clinical-trial-data/previous
      - # On failure: rollback (restore previous version)
        ssh: |
          rm -rf /var/www/clinical-trial-data/current
          mv /var/www/clinical-trial-data/previous /var/www/clinical-trial-data/current
          systemctl restart ct-data-api ct-frontend
```

**Verification:** 
- `dotnet test` passes in CI (unit + integration + HttpLive)
- After deploy, CI hits `https://<droplet-ip>/api/studies?page=1&pageSize=1` → 200 + valid shape
- CI hits `https://<droplet-ip>/` → 200
- Rollback on smoke test failure — previous version restored automatically

---

## Building & Testing Locally

### Quick Start (after PR 0)

```bash
# Start Postgres (one time, keep running)
docker compose up -d postgres

# Set connection string
export POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Database=clinical_trial_data;Username=postgres;Password=postgres"

# Build everything
dotnet build

# Run all tests
dotnet test

# Run only unit tests (no Postgres needed for most)
dotnet test Scrapers.Tests/
dotnet test Frontend.Tests/
```

### Running Tests in JetBrains Rider

**Option 1 — Set env var in Rider defaults (recommended):**
- `Run → Edit Configurations → Edit Configuration Template... → MSTest`
- Environment variables: `POSTGRES_CONNECTION_STRING=Host=localhost;Port=5432;Database=clinical_trial_data;Username=postgres;Password=postgres`
- Now every test run automatically picks it up

**Option 2 — Use `.integrationtests.env` file:**
- Create `Scrapers.IntegrationTests/.integrationtests.env`:
  ```
  POSTGRES_CONNECTION_STRING=Host=localhost;Port=5432;Database=clinical_trial_data;Username=postgres;Password=postgres
  ```
- Rider's test runner picks it up automatically (see `PostgresTestHelper.LoadFromFile()`)

**What happens during a DB test:**
1. `EphemeralPostgresDatabase` connects to Docker Postgres via Npgsql (no CLI)
2. Issues `CREATE DATABASE "ct_test_a1b2c3..."` — this is a standard PostgreSQL SQL command
3. EF Core runs migrations on the new temp database
4. Test code runs inside a transaction
5. `[TestCleanup]` rolls back the transaction, drops the temp database
6. Result: zero side effects, zero CLI dependencies

### Snapshot Tests (deterministic data)

```bash
# Default: temp DB, created + seeded + dropped
dotnet test --filter "FullyQualifiedName~SnapshotTests"

# Persist mode: DB stays around for inspection
CT_PERSIST_SNAPSHOT=true dotnet test --filter "..."
psql clinical_trial_data_snapshot  # Inspect with any SQL tool
```

---

## Verification Checklist (per PR)

```
□ dotnet restore
□ dotnet build (0 errors, 0 warnings)
□ dotnet test (all pass)
□ docker compose up -d postgres && dotnet test (full suite, no Homebrew Postgres)
□ Manual smoke test of changed pages
□ Rider test runner green (at least one DB integration test)
```

---

*This document is updated as execution progresses. Approaches that fail are noted alongside.*
