# Deploy Learnings

Every deploy failure must be documented here. Agents: update this file when you
fix or investigate a deploy issue, linking to the relevant GitHub issue and run.

## Entries

### 2026-08-12 — Legacy discovery event blocked bounded scraper recovery

- **Issue:** [#472](https://github.com/rakirs2/ClinicalTrialData/issues/472) — post-MiniLM watchdog verification reported a stale and stuck scraper.
- **Runs:** [deploy #31609814309](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31609814309), [watchdog #31612062885](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31612062885)
- **Symptom:** The instance was healthy and actively syncing, but legacy `studies.discovered` event `1` owned the queue with `596,902` records, only `400` processed, and an ETA near August 28. Its old pagination error remained in source state.
- **Root cause:** Legacy count-only discovery events cannot resume a bounded date window. Recovery handled pending legacy events only, so a stale processing event survived deployment and continued blocking bounded discovery.
- **Fix:** Worker startup now supersedes pending and processing legacy count-only discovery events, clears their in-flight progress, and prevents the stale event from being claimed again. Recovery integration coverage now includes a processing legacy event.
- **Prevention:** Every deployment restart must recover stale legacy discovery events before claiming new work; bounded discovery payloads remain the only resumable discovery format.

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

### 2026-08-11 — Pagination token expired during slow batch processing

- **Issue:** [#461](https://github.com/rakirs2/ClinicalTrialData/issues/461) — bounded pagination restarts still failed during a long ingestion batch.
- **Run:** [deploy #31419103617](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31419103617)
- **Symptom:** ClinicalTrials.gov continued returning the pagination-change 400 after the client exhausted its three whole-pagination restarts. The backfill remained pending.
- **Root cause:** The client waited for slow MeSH matching and persistence to finish before requesting the next page, leaving the page token idle for more than ten minutes.
- **Fix:** The client now prefetches the next page before invoking the current batch callback, while preserving ordered callbacks and bounded restart behavior.
- **Prevention:** Do not hold live API page tokens across slow persistence or model work; fetch the next page before processing the current batch.

### 2026-08-11 — Long stale incremental window remained unstable

- **Issue:** [#463](https://github.com/rakirs2/ClinicalTrialData/issues/463) — a 2,089-study incremental event remained unable to start after repeated pagination restarts.
- **Run:** [deploy #31509135462](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31509135462)
- **Symptom:** The source cursor was 43 hours old and the bounded restart logic still exhausted all attempts before the first batch. The full-corpus backfill remained pending.
- **Root cause:** One incremental event covered too much changing source data for a stable pagination sequence.
- **Fix:** Incremental discovery windows are now capped at 24 hours; successful events advance the cursor to the bounded window end.
- **Prevention:** Never allow an unresolved incremental event to grow into an unbounded historical window.

### 2026-08-11 — System Status telemetry timeouts under event volume

- **Issue:** [#465](https://github.com/rakirs2/ClinicalTrialData/issues/465) — System Status telemetry was unreliable on the instance.
- **Runs:** [watchdog #31511239808](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31511239808), [watchdog #31508153869](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31508153869)
- **Symptom:** `/api/event-queue/stats` repeatedly exceeded the 30-second watchdog limit; `/api/data-source-state` also failed during some load/deployment windows. The Status page could retain incomplete telemetry.
- **Root cause:** Queue stats ran multiple full-table aggregates and an unbounded historical per-type average on every request, while concurrent Status/watchdog requests had no shared response cache.
- **Fix:** Group status counts into one query, bound per-type averages to the recent sample, serialize and cache the stats response for 15 seconds, and render explicit unavailable states for failed telemetry sections.
- **Prevention:** Keep operational telemetry bounded and cached; never let one endpoint failure suppress or masquerade as another telemetry section.

### 2026-08-11 — Queue telemetry remained serial after first optimization

- **Issue:** [#465](https://github.com/rakirs2/ClinicalTrialData/issues/465) remains open for follow-up.
- **Run:** [deploy #31516309931](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31516309931)
- **Symptom:** `/api/event-queue/stats` continued to exceed the watchdog budget after bounded aggregation and caching were deployed.
- **Root cause:** The endpoint still awaited the queue summary and event-type breakdown queries serially, so their latencies accumulated.
- **Fix:** Follow-up change runs both independent reads concurrently while retaining the short cache.
- **Prevention:** Operational telemetry endpoints must parallelize independent database reads and remain bounded/cached.

### 2026-08-11 — Request-path queue telemetry still timed out

- **Issue:** [#465](https://github.com/rakirs2/ClinicalTrialData/issues/465) remains open for follow-up.
- **Run:** [deploy #31518918726](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31518918726)
- **Symptom:** The parallel-query optimization still produced watchdog timeouts because the first request waited for the database work to complete.
- **Root cause:** A request-path cache cannot protect the first request after restart or cache expiry.
- **Fix:** Move telemetry refresh to a hosted background service and make the endpoint return the latest snapshot immediately with explicit health metadata.
- **Prevention:** Health endpoints must never synchronously depend on slow operational aggregates; refresh them asynchronously with bounded cancellation.

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
