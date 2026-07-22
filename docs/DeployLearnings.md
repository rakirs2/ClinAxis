# Deploy Learnings

Every deploy failure must be documented here. Agents: update this file when you
fix or investigate a deploy issue, linking to the relevant GitHub issue and run.

## Entries

### 2026-07-21 — Stats API smoke test fails on deploy

- **Issue:** [#249](https://github.com/rakirs2/ClinicalTrialData/issues/249) —
  "[P0] Deploy: /api/stats smoke test fails — DB not ready when /health returns Healthy"
- **Run:** https://github.com/rakirs2/ClinicalTrialData/actions/runs/29859884747
- **Symptom:** Deploy script times out polling `/api/stats` (10 retries × 3s = 30s).
  Exits with `FATAL: API stats endpoint failed`.
- **Root cause:** The `/health` endpoint had no DB connectivity check — just bare
  `AddHealthChecks()` with no registered checks. It returned "Healthy" the instant
  the process started, regardless of whether the database was reachable. The deploy
  flow would then proceed to the `/api/stats` smoke test, which has a 30s timeout.
  When the DB connection pool wasn't ready yet, the stats endpoint couldn't respond.
- **Fix:** Added `DatabaseHealthCheck` (custom `IHealthCheck` at
  `DataApi/DatabaseHealthCheck.cs`) that calls `Database.CanConnectAsync()`
  with a short timeout. Registered via `AddCheck<DatabaseHealthCheck>` in
  `DataApi/Program.cs`. The 60s health-wait loop now waits for actual DB readiness,
  so `/api/stats` succeeds on the first call after health passes.
- **Prevention:** All deploy smoke tests now verify real DB readiness (not just
  process liveness) before considering the service healthy. Any future deploy
  failure must be logged as a new entry in this file.
