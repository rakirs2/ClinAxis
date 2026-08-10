# Deploy Learnings

Every deploy failure must be documented here. Agents: update this file when you
fix or investigate a deploy issue, linking to the relevant GitHub issue and run.

## Entries

### 2026-08-10 — Post-deploy ClinicalTrials.gov pagination invalidation

- **Issue:** [#452](https://github.com/rakirs2/ClinicalTrialData/issues/452) — scraper stalled after the MeSH optimization deploy.
- **Run:** [deploy #31405292209](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31405292209)
- **Symptom:** Containers and `/health` were healthy, but the ingestion run had processed 0 batches and 0 records. The source state reported `syncing` with a stale cursor and the error `ClinicalTrials.gov returned 400 (BadRequest): The data have probably changed while you were paginating.`
- **Root cause:** ClinicalTrials.gov invalidated a pagination token while the client was traversing a changing result set. The client retried the invalid token instead of restarting pagination from the first page.
- **Fix:** `ClinicalTrialsGov.GetTrialRecordsBatchedAsync` now recognizes this specific response, restarts from page one up to three times when no batch callback has run, and lets the existing event retry path handle invalidation after persistence has begun.
- **Prevention:** Keep pagination-change handling distinct from ordinary HTTP retries, bound whole-pagination restarts, and never replay a persisted batch callback inside the same ingest call.

### 2026-08-10 — Long-running discovery event reclaimed during ingestion

- **Issue:** [#458](https://github.com/rakirs2/ClinicalTrialData/issues/458) — active ingestion was repeatedly reset before the backfill could run.
- **Run:** [deploy #31416461452](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31416461452)
- **Symptom:** The `studies.discovered` event processed batches but was reclaimed after the ordinary 30-minute claim timeout. Its progress reset, the distinct database count stayed flat because the event updated existing studies, and the 95,950-study backfill remained pending.
- **Root cause:** `ReleaseStuckEventsAsync` used only `ClaimedAt`; it ignored the live `ProgressUpdatedAt` heartbeat written after each completed batch.
- **Fix:** Claim release now uses the latest progress heartbeat, falls back to claim time before the first heartbeat, and clears stale progress fields on a new claim.
- **Prevention:** Long-running events must heartbeat their claim lease; a claim timeout must measure inactivity, not total elapsed processing time.

### 2026-08-07 — Recovery workflow consumed retry-list stdin

- **Runs:** [dry run #31226197920](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31226197920),
  [retry #31226348515](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31226348515)
- **Symptom:** The recovery workflow selected five valid backfill events but reported only one
  retry.
- **Root cause:** The SSH command inherited the retry-list file as stdin, so the first remote
  curl consumed the remaining event IDs.
- **Fix:** Added `ssh -n` so remote recovery commands cannot consume the local retry list.
- **Prevention:** Recovery runs report selected and retried counts; dry-run remains the required
  first step.

### 2026-08-07 — Watchdog event queue telemetry timed out

- **Issue:** [#406](https://github.com/rakirs2/ClinicalTrialData/issues/406) —
  "Watchdog: pipeline stalled or DLQ over threshold"
- **Run:** [watchdog #31224242921](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31224242921)
- **Symptom:** The watchdog could reach source and scraper endpoints, but the event-queue
  stats request exceeded its 30-second limit and was reported as an unparseable payload.
- **Root cause:** Event telemetry loaded every completed event duration into application memory
  for averages and percentiles. The historical queue made that query too slow for the watchdog.
- **Fix:** Bound duration calculations to the most recent 10,000 completed events while retaining
  exact queue counts and event-type aggregates.
- **Prevention:** Keep operational telemetry queries bounded; treat the watchdog endpoint timeout
  as an alert condition rather than silently dropping queue health.

### 2026-08-07 — Deploy scrape gate rejected active recovery

- **Issue:** [#406](https://github.com/rakirs2/ClinicalTrialData/issues/406) —
  "Watchdog: pipeline stalled or DLQ over threshold"
- **Run:** [deploy #31220748924](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31220748924)
- **Symptom:** All images built, containers became healthy, and the deployment stopped
  with `FATAL: lastSyncTimestamp ... was 42m old (>30m) after deploy`.
- **Root cause:** The deployment gate assumed the cursor must advance within two minutes.
  The new unresolved-event guard correctly leaves the cursor unchanged while a prior
  discovery event is being recovered, so the gate treated active recovery as failure.
- **Fix:** Update the gate to accept a recent `syncing` source state while continuing to
  reject stale `idle`, `failed`, or missing states. No blind redeploy was performed.
- **Prevention:** Deployment validation now distinguishes active recovery from a dead
  scraper; watchdog queue and source checks remain the authority for backlog health.

### 2026-08-07 — Scraper recovery deploy and stale event backlog

- **Issue:** [#406](https://github.com/rakirs2/ClinicalTrialData/issues/406) —
  "Watchdog: pipeline stalled or DLQ over threshold"
- **Runs:** [deploy #31218130121](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31218130121),
  [watchdog #31219300327](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31219300327)
- **Symptom:** Production was running PR #421 while PRs #422 and #423 were already merged.
  The post-deploy watchdog could reach all health endpoints, but still reported
  `deadLetterCount=5458`.
- **Root cause:** The deployed image did not contain the scraper-loop backoff or
  non-destructive split-query re-ingest fixes. Independently, incremental discovery
  events stored only a count and advanced the source cursor before downstream ingestion
  completed. Existing dead letters are historical queue state and are not replayed by a
  normal deploy.
- **Fix:** Deployed current `main` at `de4d5e8` with `reset_db=false`; the deployment's
  pipeline scrape validation passed. The follow-up scraper fix carries bounded discovery
  windows, acknowledges the cursor after successful ingestion, preserves legacy payloads,
  and enforces persisted retry backoff.
- **Prevention:** Treat deployment health and queue recovery as separate checks. Retry only
  `studies.discovered` and `studies.backfill` dead letters in a controlled operation; do not
  bulk-retry the historical enrichment dead-letter population.

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
