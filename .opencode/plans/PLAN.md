# Deploy Reliability Improvements

## Problem
Production DataApi crash-looping because:
1. Old DataApi process survived `systemctl stop` (no `KillMode=mixed`)
2. `fuser -k 5003/tcp || true` swallowed port-free failure
3. `DROP SCHEMA public CASCADE` ran underneath old process's connections → stale DB state
4. Subsequent restarts failed with "address already in use" (counter reached 1164+)

## Approach (deployed successfully 2026-07-21)
Manually killed the orphan DataApi PID (1983866) holding port 5003. This freed the port and allowed the new DataApi from systemd restart to start fresh.

## Preventative Changes (this PR)

### Systemd changes
- Added `KillMode=mixed` to all 3 service units — ensures ALL processes in cgroup die on stop
- Simplified `ExecStop` to just `kill -TERM $MAINPID` (KillMode handles rest)
- Increased `TimeoutStopSec` from 15 to 30

### Deploy script
- Created `deploy/deploy.sh` — standalone deploy orchestrator
- Created `deploy/reset-db.sh` — manual database reset (droplet only)
- Migration runs **before** stopping services (old code still serves)
- Port-free verification polls `ss -tlnp`, escalates to SIGKILL, **exits 1** if port still held
- Health check validates both `/health` and `/api/stats` (actual DB queries)

### CI workflow
- Replaced ~200 lines of inline SSH heredocs with single `bash deploy/deploy.sh` call
- Removed `DROP SCHEMA public CASCADE` from automated deploy
- Removed `fuser -k` (replaced by deterministic polling)
- Removed `|| true` error suppression

### Connection resilience
- Added `EnableRetryOnFailure(3, 5s)` to all 3 `UseNpgsql` call sites

## Decision Log
- Chose standalone deploy.sh over inline CI heredocs for testability and single source of truth
- Chose `KillMode=mixed` over manual `ExecStop` fuser cleanup (systemd-native)
- Chose additive-only deploys (no DROP SCHEMA) — resets are manual via `--reset-db` flag

---

# Docker Deploy Experiment (Issue #252)

## Progress
- PR #251 merged: Dockerfiles + docker-compose.yml + deploy script tweaks
- PR #253 merged: `.github/workflows/deploy-docker.yml` (experimental workflow)
- PR #254 merged: lowercase image names (rakirs2/clinicaltrialdata-*)
- PR #256 merged: `permissions: {contents: read, packages: write}` for GHCR push
- PR #257 merged: docker compose plugin install on droplet (via direct binary download)
- PR #258 merged: ditto (hotfix on same branch)
- PR #259 merged: write .env file via scp instead of inline SSH env vars
- PR #260 merged: ensure Docker is installed + use sudo for all docker commands
- PR #261 merged: start Docker daemon properly + .env before pull
- PR #262 merged: install curl in DataApi image for health check
- PR #263 merged: SSH retry logic (3 attempts) for transient failures

## Failures (Run 1 of 3 — 8 attempts so far)
| Attempt | Root Cause | Fix |
|---------|-----------|-----|
| 1 | Uppercase image tag (rakirs2/ClinicalTrialData-*) | Lowercased (#254) |
| 2 | Missing packages:write permission | Added permissions block (#256) |
| 3 | `docker compose: unknown command` on droplet | Download compose binary (#257) |
| 4 | `POSTGRES_CONNECTION_STRING` not reaching Docker Compose | .env file via scp (#259) |
| 5 | `permission denied: /var/run/docker.sock` | sudo + install Docker (#260) |
| 6 | Docker daemon not running | Explicit systemctl restart + sock wait (#261) |
| 7 | data-api unhealthy — curl not in runtime-deps image | Install curl in Dockerfile.api (#262) |
| 8 | SSH connection timeout after Docker containers left running | SSH retry logic + droplet power cycle |

## Current state
**Droplet power-cycled, production restored via systemd (2026-07-22).** Run 9 about to begin with all fixes in place: curl in image (#262), SSH retry (#263), .env before pull (#261), proper daemon start (#261), sudo for docker (#260).

## Remaining unknowns
- With the droplet fresh: can the full pipeline complete?
- `network_mode: host` containers from a failed run might interfere with SSH on subsequent runs
- 3 consecutive successful runs required before replacing systemd as primary

---

# Testing Overhead Reduction (Issue #327)

## Context
Frontend bUnit tests flake under machine load in full-solution runs; 148 integration tests all require Docker. Three-part plan: PR C (extract pure logic → unit tests, prerequisite), PR A (frontend pure helpers), PR B (pyramid reset 148 → ~17).

## PR C — Extract ingestion/feature logic into pure helpers (issue #328)
- Extracted `KeywordFilter`, `AffiliationFilter`, `ConditionDecision`, `PersonNameKey` into `Scrapers/Utilities/` (verbatim moves from `StudyRepository`).
- 25 new unit tests (9 keyword, 5 affiliation, 3 condition-decision, 8 person-name-key) + 8 InvestigatorScorer edge tests.
- Constraint honored: all 148 integration tests stay green, zero deletions; the 5 redundant StudyRepositoryTests cases are the safety net and are removed in PR B.
- AGENTS.md: added §3b "Current Testing Approach — Baseline Log" (to be superseded by PR B's §3/§6/§7 rewrite).

## Decisions / gotchas
- `ConditionDecision` returns anonymous objects to preserve the exact `RejectedConditions` JSONB shape (cui_not_found has meshCui+similarity; plain rejection has neither).
- Collection expression `conditions: ["cancer"]` target-typed to `HashSet<string>` uses the case-SENSITIVE default comparer — production passes `ToHashSet(StringComparer.OrdinalIgnoreCase)`. Unit test fixture must pass an explicit OrdinalIgnoreCase HashSet (failure mode: condition-dup keywords leak into cleaned list).
- Issue specs referenced `PiFeaturesExportEndpoints` line numbers from the pre-#326 main state; verified against current main before extracting.

## PR A — Frontend pure helpers + deterministic bUnit (issue #329)
- Extracted `SearchConditionFilter`, `SearchCriteria` (internal sealed), `SearchQueryBuilder`, `PaginationHelpers` into `Frontend/`; wired into Search.razor + DataQuality.razor.
- 27 new pure unit tests; deleted the 2 autocomplete bUnit tests (now pure unit coverage); made `ConditionAutocompleteFiltersWhenTypedBeforeConditionsLoad` deterministic via a TaskCompletionSource-gated MockHttp response instead of `Task.Delay(300ms)`.
- Keep scope: pagination copies remain in Investigators/Investigator/DeadLetter/PipelineHistory (out of issue scope).

## Gotchas (Frontend.Tests)
- CA1707 (underscores) is NOT suppressed in Frontend.Tests (Scrapers.Tests suppresses it) — use camelCase test names there.
- CA1307: StringAssert.Contains / string.Contains need StringComparison.Ordinal.
- CA1305: int.ToString needs CultureInfo.InvariantCulture.
- `string.Join(",", List<int?>)` renders null as empty string — map to "null" explicitly in assertions.

## PR B — Test pyramid reset (issue #330)
- Integration suite: 148 tests / 27 classes → **17 tests / 8 classes**:
  - Survivors: MigrationIntegrationTests (2), SchemaGuardTests (4, new — folds PerformanceTests index guards + AggregationSchemaGuardTests + DataLossRemediation schema checks), DataLossRemediationTests (1), StudyRepositoryHappyPathTests (3, new — replaces StudyRepositoryTests/StudyListingSnapshotTests/InvestigatorsApiTests/InvestigatorMetricsIntegrationTests), FullPipelineIntegrationTests (1), DataApiSmokeTests (3, adds seeded search + investigator-finder from DataApiSearchE2ETests), FrontendE2ETests (2, folds FrontendSmokeTests /health), PiFeaturesExportTests (1 happy path).
  - Deleted 21 redundant files: AdvancedSearchApi, AggregationIntegration, AggregationSchemaGuard, ClinicalTrialsIngestion, DataApiSearchE2E, DebugDbConnection, E2EPipeline, FiftyTrialMapping, FrontendSmoke, InvestigatorMetrics, InvestigatorsApi, MedicareProcedure, MedicareUtilization, NpiDisambiguation, OpenPayments, Performance (latency benchmarks — flaky by nature), PipelineRunner, PubMedScraperDb, StudyListingSnapshot, StudyRepository, TrainingDataExport.
- Scrapers.Tests is now DB-free: deleted AdvancedSearchRepository (13 — DB suite disguised as unit tests), InvestigatorFinderRepository (2), MeshDescriptorSeeding (4); PubMedScraperServiceTests reduced to 5 pure parse tests (dropped DbTestBase base + 1 DB test).
- DataApi.Tests: +5 `PiFeaturesExportValidationTests` after making `ValidateWindow` internal (unit-mirrors the 5 deleted BadRequest integration tests).
- CI: dotnet.yml `live-http-tests` job deleted (its `TestCategory=HttpLive` matched zero tests anywhere); stale `--filter "TestCategory!=HttpLive"` dropped; single `unit-and-db-tests` job runs all four test projects.
- AGENTS.md: §3 rewritten as testing-pyramid doctrine (integration suite is a reviewable budget), §7/§8 updated, GitHub Actions section refreshed; the PR-C-added §3b baseline log is superseded here.

## Gotchas (PR B)
- dotnet.yml splice via python slice duplicated the job block — rewrote the file cleanly with the Write tool instead.
- DataApi minimal APIs serialize with the web defaults (camelCase) — investigator-finder smoke test reads `totalCandidates`/`investigators`/`uuid`, not PascalCase.
- DataApi.Tests NoWarn includes CA1707 (PascalCase test names fine there, unlike Frontend.Tests).
- Integration test count: 2+4+1+3+1+3+2+1 = 17.

## PR A fix — SearchPageRendersPagination CI flake (post-#331 merge)
- `SearchPageRendersPagination` (pre-existing, 6s `WaitForState`) timed out in PR A's CI run (`unit-and-db-tests`), the documented bUnit flake class from issue #327. Passes 10/10 locally in ~250ms — timing, not logic.
- Fixed deterministically: added `TaskCompletionSource? studiesGate` to `SetupTest` (mirror of the existing `conditionsGate`), gate the `/api/studies*` mock, click Search, assert no table before the gate completes, then `SetResult()` + `WaitForState` (now resolves instantly). Same pattern as the autocomplete test conversion.
- Verified: 3 consecutive full Frontend.Tests runs, 77/77 green, 0 warnings.

---

# NPI Disambiguation — Precision-First Scorer (PR: feature/npi-rule-scorer)

## Problem
Status-page "Ambiguous (2+ matches)" count was high. Old cascade in `InvestigatorEnrichmentService` (affiliation substring → ORCID → state → else ambiguous) rarely resolved: NPPES org names ≠ CT.gov institution strings, ORCID often absent, state tie-break requires exactly 1. Worse, `NppesNpiRegistryClient` pre-narrowed results via `state=` param + org filter, so the full candidate set was never seen. A deleted experiment (`experiments/NpiDisambiguation/`, source unrecoverable — PDB pointed to a force-pushed commit) had scored candidates with 7 signals but was never shipped.

## Approach (this PR)
- **Migration** `NpiCandidateEnrichmentFeatures`: `person_identifier_candidates` += matched_middle_name/credential/name_prefix/gender/city/taxonomy_desc/taxonomy_state/taxonomy_license/other_names_json/identifiers_json + rule_score (additive-only; §6 data-loss fix — NPPES fields previously discarded).
- **`NpiFeatureExtractor`** (Scrapers/Utilities, pure): builds `PersonSignalProfile` (name parts via NameParser, ORCID, primary affiliation, specialty categories derived from MeSH descriptor names of the person's studies) + 12 per-candidate match features (exact name, middle, credential, state, city, org substring, other-name, taxonomy-vs-specialty, license state, department, ORCID, deactivated). Single mapping source for persistence + scoring + future model.
- **`NpiCandidateScorer`** (pure): weighted score = matched/applicable (missing data abstains, not penalizes); deactivated = 0. Resolution: empty → not_found; all-deactivated → ambiguous; single ORCID identifier (type "17") match → hard override; unique best ≥ threshold + corroboration (state/city/org/other-name/specialty/license/department/ORCID) → assigned; single active candidate needs only name plausibility (≥0.5) — preserves old single-result behavior; ties → ambiguous.
- **Client**: `SearchByNameAsync(first, last, ct)` — affiliation/state narrowing removed; scorer sees the full set.
- **Backfill**: `StudyRepository.RequeueAmbiguousNpiLookupsAsync` resets `npi_lookup_attempted_at`/`npi_enrichment_result` for ambiguous persons, deletes stale NPI candidates, enqueues `investigator.enrichment` (skips persons with a live pending event — they get reset anyway so their pending event re-runs them). Endpoint: `POST /api/enrichment/requeue-ambiguous`.
- **Budget swap**: integration suite stays 17 → replaced `StatsCounts_MatchDatabaseState` (vacuous) with 2 requeue tests (17 - 1 + 2 = 19... superseded by decision below).

## Decisions / gotchas
- **Person city/state = primary affiliation only** (not study locations): study sites are network facilities, weaker signal; kept high-precision.
- **Corroboration rule** (auto-assign requires an independent signal beyond the name) is the accuracy guarantee; user explicitly chose precision-first over recall, review UI skipped for now, in-DB signals only (no on-demand CMS fetches).
- **ML track is a separate PR** (`feature/npi-disambiguation-model`): same extractor/feature contract; corpus export + logistic regression → ONNX (MeSHMatcher pattern); model runs in PARALLEL with the rule scorer recording `ModelScore` per candidate for A/B comparison (user requirement).
- InternalsVisibleTo: added `IngestionApp` to Scrapers.csproj (existing pattern, avoids public API surface).
- CA1826: use loops/indexes instead of `FirstOrDefault(predicate)` on indexable collections.
- ExecuteUpdateAsync does NOT refresh tracked entities — integration tests must query `AsNoTracking()` for post-update assertions.
- Specialty keyword map: "General Practice" matches genetics via substring "gene" — test fixtures must avoid accidental keyword substrings; "oncology"/"oncolog" must be explicit keywords ("Medical Oncology" text has no "cancer"-family substring).
- Deactivated ORCID candidate does NOT override (filtered from active set before the override check).
- Integration suite total: 19 (17 - 1 replaced + 2 requeue tests + 1 schema guard). Schema guards are additive-encouraged; behavioral swap kept the count at 17 + guard.

## Verification
- `dotnet build -c Release`: 0 errors, 0 warnings.
- Full local suite: Scrapers.Tests 194/194, DataApi.Tests 88/88, Scrapers.IntegrationTests 19/19 (Docker Postgres via Testcontainers), Frontend.Tests 77/77.

---

## Attempt log — #357 Structured logging (feature/357-structured-logging)

**Date:** 2026-08-02

### What was done
- Replaced all 9 remaining `System.Diagnostics.Debug.WriteLine` sites (no-op in Release builds) with `LoggerMessage.Define` + `ILogger<T>` constructor injection across:
  - `DeadLetterProcessingService` (3 sites: DLQ count warning, per-event samples, loop error)
  - `MedicareUtilizationService` (2), `InvestigatorMetricsService` (2), `OpenPaymentsService` (1), `InvestigatorEnrichmentService` (1 + added logs to previously-silent swallow-all catches), `PivotServiceRegistry` in Scrapers (1)
- `IngestionApp/Program.cs`: `.ConfigureLogging(AddJsonConsole)` + loggers wired into all service registrations; log levels configurable at runtime via `Logging__LogLevel__*` env vars (Host.CreateDefaultBuilder reads them natively).
- `Scrapers.csproj`: explicit `Microsoft.Extensions.Logging.Abstractions` package ref (was transitive).
- `IngestionApp.csproj`: `InternalsVisibleTo("Scrapers.Tests")`; `Scrapers.Tests.csproj`: ProjectReference to IngestionApp.
- Docs: `docs/README.md` "Ingestion Logs" section (docker logs + Logging__* config).

### Tests
- `NoDebugWriteLineGuardTests` (2 tests): IL-scan guard asserting zero calls to `System.Diagnostics.Debug` members in IngestionApp and Scrapers assemblies — prevents regression.
- `DeadLetterProcessingServiceTests` (2 tests): in-memory `IEventQueueService` + capturing `ILogger`; asserts DLQ-count warning + sample events on non-empty DLQ, and error log when the queue throws.

### Verification
- `dotnet build` 0/0, Scrapers.Tests 248/248 (4 new).
- Full suite verification pending (integration + Frontend + DataApi).

### Note
- Stashed stale local WIP (`git stash -u`): an earlier draft of the NPI ML-track feature that PRs #340/#341 already merged differently on origin/main. Preserved as `stash@{0}`; do NOT restore unless explicitly requested.

---

## Attempt log — #358 Stall/DLQ alerting (feature/358-stall-dlq-alerting)

**Date:** 2026-08-02

### What was done
- `scripts/health-check.sh`: pure bash + python3 check functions (dead-letter count > 10,
  lastSyncTimestamp older than 48h, status != idle for > 6h) reading endpoint JSON from
  files; exit 0 healthy / 1 alert with a templated markdown body (findings + raw payloads).
  Thresholds env-overridable; source-guarded so tests can import the pure functions.
- `scripts/test-health-check.sh`: 23 offline unit tests (generated fixtures with relative
  timestamps, full-script exit codes, --alert-file, missing-payload path). Wired into
  dotnet.yml CI as a no-Docker step.
- `.github/workflows/watchdog.yml`: schedule cron every 3h + workflow_dispatch; SSHes to
  the droplet via DEPLOY_* secrets, curls localhost:5003 endpoints, evaluates, opens a
  `watchdog`-labeled GitHub issue on failure (quiet period = an open issue suppresses
  duplicates), auto-closes the issue when healthy again. Thresholds via repo variables.
- Docs: docs/README.md "Watchdog Alerts" section (trigger/acknowledge/tuning).

### Decisions
- Files-in (not curl-in) script interface: workflow fetches over SSH; tests use fixtures.
- No cross-run state: no-progress detection uses timestamp staleness (per issue: #349 rate
  tracking landed, but delta-between-runs needs persisted state — kept stateless).
- Endpoint fetch failure (SSH/curl down) is itself an alert.
- No heartbeat (user choice).

### Verification
- `bash scripts/test-health-check.sh`: 23/23.
- `bash scripts/validate-workflows.sh`: all pass (actionlint, line length, indentation, sudo).
- No C# changes on this branch; dotnet suites unaffected.

### Prior attempt (superseded)
- Earlier plan considered curl-stubbing inside the script; replaced with the files-in
  interface for deterministic testing. Logged per AGENTS.md §11.

---

## Attempt log — #364 DLQ investigation + enrichment NPI-collision fix (feature/enrichment-npi-collision-fix)

**Date:** 2026-08-03

### Investigation (live instance, watchdog issue #364)
- Read the live DLQ (`/api/event-queue/dead-letter`): 195 events, all created after the
  19:14Z reset+backfill deploy. Two classes:
  1. **193 × investigator.enrichment** — generic `DbUpdateException` (inner exception not
     stored — enrichment fail sites used `ex.Message`, EventProcessingService used
     `ex.ToString()`). Verified root cause via API: dead-lettered "Robert L Murphy" vs
     existing "Robert Murphy" row with NPI 1558476416 (same Northwestern affiliation) —
     duplicate person rows (dedup by exact FullName only, StudyRepository.cs:1746) both
     match the same NPPES record; second `person.Npi` assignment violates the unique
     filtered index `IX_investigator_persons_npi` → 23505 → deterministic, 4 attempts
     each (retryCount=3), dead-letter. Not a stall — enrichment is keeping up
     (27.9k completed).
  2. **2 × studies.discovered (count 1011)** — managed `OutOfMemoryException` inside the
     EF `BufferedDataReader` of the 500-study batch-prefetch (StudyRepository
     `UpdateStudiesWithClinicalTrialsAsync`). The 384m→768m mem_limit bump (#385) was
     already deployed and insufficient; container also runs openpayments (10-way) +
     enrichment + medicare + metrics.
- The 08-01→08-02 "syncing" stall was the pre-backfill era: giant incremental
  `studies.discovered` events + 60-min ingest timeout (#359) = fail/retry loop; DB was
  reset (reset_db=true) at the 19:14Z deploy to launch the backfill engine (#394).

### Fix (this branch)
- **`NpiCollisionDetector`** (Scrapers/Utilities, pure): classifies 23505 on the npi
  index from a `DbUpdateException` (walks inner chain; constraint-name guard).
- **`InvestigatorEnrichmentService`**: on collision → `Npi=null`,
  `NpiEnrichmentResult="duplicate"`, re-save, skip downstream enqueues (event completes
  instead of dead-lettering); fail sites now store `ex.ToString()` (DLQ-diagnosable).
- **`Program.cs`**: `CT_GOV_PAGE_SIZE` default 500 → 200 (user chose batch reduction
  over another mem_limit bump — host RAM unknown, 2 vCPU droplet; #385's 768m already
  insufficient for 500-study buffered reads under concurrent load).
- **AGENTS.md §9**: 384m → 768m for ingestion (stale doc; compose already 768m).

### Tests
- 7 unit tests (`NpiCollisionDetectorTests`) — 23505/npi-index, null constraint,
  nested inner, other constraint, other SqlState, non-PG inner, no inner.
- 1 integration test (`Enrichment_DuplicatePersonNpiCollision_CompletesWithoutDeadLettering`)
  — verified it fails with the exact production error
  `23505: duplicate key value violates unique constraint "IX_investigator_persons_npi"`
  against the pre-fix code, passes with the fix.
- Full suite Release: 333 Scrapers.Tests + 99 DataApi.Tests + 38 Integration + 92
  Frontend = 562/562, 0 warnings.

### Decisions / gotchas
- `PostgresException` 4-arg ctor is `(messageText, severity, invariantSeverity, sqlState)`
  — passing sqlState in the 4th slot produces a wrong SqlState; use the 18-arg ctor with
  named args in tests (`constraintName` is a positional arg there; `SqlState` maps to the
  4th param, `ConstraintName` is read-only — init only via ctor).
- Kept the duplicate row's NPI candidates persisted (data preservation, §6); they are
  legitimate NPPES matches.
- OOM remains a watch item: only 2 events, deterministic fix is the smaller batch; if it
  recurs, next lever = mem_limit 1g (needs host RAM ≥ 4GB) or splitting openpayments out
  of the ingestion container.

### Deploy follow-ups
- Retry the 193 enrichment dead letters (POST /api/event-queue/dead-letter/{id}/retry)
  after deploy — the 2 OOM'd discovered events are also safe to retry.

---

## P2 — Filter-based search (issue #27, #30) — PRs #408–#411

**Date:** 2026-08-05

### What was done
- **#408 (refactor)** — extracted the duplicated `/api/studies` filter predicates (`SearchStudiesAsync`/`CountStudiesFilteredAsync`) into `Scrapers/Utilities/StudySearchFilter.cs`; count previously omitted `LocationMeshTreePrefixes` → wrong totalPages (fixed by construction). 15 unit tests; fixed the vacuous smoke-test param (`search=` → `keyword=`).
- **#409 (feat)** — keyword filter also matches `study_keywords` (table + index existed, never searched).
- **#410 (feat, #27)** — real-time + shareable URL: `SearchUrlParser` (round-trips `SearchQueryBuilder` output), `NavigationManager` URL sync (`replace: true`), 400ms debounce for text inputs, instant search for toggles; auto-search on load when URL has filters; `SearchQueryBuilder` now sends all cities. 8 parser tests + 4 bUnit page tests.
- **#411 (fix, #30)** — MeSH-location mode passed Z-tree numbers as country/state to `/api/distinct-locations`; now resolves descriptor NAMES in the cascade, and the endpoint normalizes params via `LocationNormalizer` (California→CA) so names match the normalized DB values. 1 bUnit test. #30 closed as region-based.
- Docs: #407 (sync through #405), #412 (P2 status); issue #382 updated, #27/#30 closed.

### Gotchas / decisions
- **EF translation probe (throwaway console + `ToQueryString()`):** EF Core 10 translates plain `ToLower().Contains()` → `LOWER() LIKE` and `StartsWith(p)` → `LIKE 'p%'`, but throws on ALL `StringComparison` overloads, `ToLowerInvariant()`, and `ToLower(CultureInfo)`. CA1304/1307/1310/1311/1862 therefore suppressed path-scoped in `.editorconfig` (repo convention) with rationale comment.
- **Fire-and-forget `_ = SearchStudies()` from sync handlers deadlocks bUnit renders** — continuation resumes on the thread pool; `StateHasChanged` never reaches the renderer (spinner stuck). Fix: all handlers `async Task` + `await`. The debounce path is fine because the `Task.Delay` continuation is posted to the renderer sync context.
- **bUnit stale-element race under load:** interacting right after `Render()` can hit `UnknownEventHandlerIdException` (async `OnInitializedAsync` re-renders). Fix: `WaitForState` for the initial-load indicator ("Loading conditions..." gone) before interacting; skipped when a conditions gate is in play (the gated test needs pre-load state).
- **bUnit 2.8 type is `BunitNavigationManager`, not `FakeNavigationManager`** (that's the 1.x name).
- **`gh issue edit` body wipe:** a shell command-substitution failure passed an empty body and wiped issue #382's body; restored from session snapshot. Lesson: capture bodies to a file and verify before piping into `gh issue edit`.
- bUnit's `Input()`/`Change()` set value + fire the event once — two rapid `.Input()` calls correctly exercise the debounce (first is cancelled by `CancellationTokenSource`).
- Frontend.Tests does NOT suppress CA1707/CA1861 (unlike Scrapers.Tests) — camelCase names, static readonly fixture arrays.

---

## Attempt log — scraper unblock (feature/unstall-scraper)

**Date:** 2026-08-07

### Investigation

- GitHub `main` was two commits ahead of the local checkout: PR #422 added scraper-loop backoff and PR #423 added split-query, non-destructive re-ingest.
- Production's last successful Docker deploy was SHA `5339f4b` (PR #421); the recovery deploy for current main is run [#31218130121](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31218130121), with `reset_db=false`.
- The discovery loop counted a bounded update window but persisted only the count, fetched the listing head during processing, and advanced the cursor before ingestion completed.
- Queue retries recorded `LastErrorAt` but claimed failed events immediately, causing retry storms and premature dead letters.

### Changes

- Added bounded incremental discovery payloads with legacy count-only parsing for already-persisted production events.
- Added active-event deduplication and moved successful cursor acknowledgement into event processing.
- Added persisted retry eligibility using 30-second, 2-minute, and 10-minute delays; dead lettering now follows the documented three-retry schedule.
- Added end-to-end queue, bounded-fetch, cursor-acknowledgement, and payload tests without increasing the integration test class count.
- Removed the root `.opencode/` ignore rule so the planning log is visible to Git.

### Verification

- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: 643 passed.
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release`: 643 passed.

### Production recovery

- Recovery deploy completed successfully with `reset_db=false`; the workflow's pipeline scrape validation passed.
- Manual watchdog run [#31219300327](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31219300327)
  reached the production endpoints and reported `deadLetterCount=5458`.
- The historical dead-letter backlog remains and requires a controlled study-event retry operation;
  the deploy intentionally did not bulk-replay enrichment failures.

### Review follow-up

- Added unresolved-window detection so a bounded dead letter cannot generate a duplicate event every hour.
- Made source cursor writes monotonic, rejected partial discovery ingestion before cursor acknowledgement,
  and isolated source-status failures from queue retry persistence.
- Re-ran Release verification after the hardening changes: 643 tests passed with 0 warnings and 0 errors.

---

## Attempt log — P1 legacy discovery backlog recovery

**Date:** 2026-08-07

- Watchdog showed 1,453 pending legacy count-only `studies.discovered` events blocking the
  full-corpus backfill, plus 24 pending backfill events.
- Added a dry-run-first recovery endpoint and workflow that coalesces only pending legacy
  count-only discovery events, preserving bounded discovery, backfill, processing, and malformed events.
- The first recovery run retried one of five chunks because SSH consumed the retry list stdin;
  fixed with `ssh -n`, then retried the remaining four successfully.
- Current watchdog issue #430 shows backfill dead letters at zero, legacy discovery dead letters
  retained at ten, and the source actively processing the remaining queue.

---

## Attempt log — Backfill throughput investigation + first speedup PR (feature/speed-up-backfill)

**Date:** 2026-08-08

### Investigation (live watchdog issue #433, code audit, local benchmark)

- Live numbers: `studies.backfill` 17 pending / 1 processing, ~100,721 remaining,
  `ingestRatePerHour=2085`, last 200-record root batch `lastBatchDurationMs=379,996`
  (≈6.3 min/batch), est. completion 2026-08-10.
- **Local benchmark** (throwaway project in /tmp, real model + real CT.gov fixture terms):
  - Cold (no cache) match cost: **185.8 ms/term** single-shot on this Mac
    (MeshBench synthetic set: 99 ms/term). Droplet (2 vCPU) ≈ 300-500 ms/term.
  - Per 200-record batch ≈ 4-6k Match calls, most unique → 380s/batch explained
    **fully by MeSH matching** (inference + 61,794 × 768 cosine scan per unique term).
  - ONNX model input is `-1x128`: batch dim dynamic, **sequence length FIXED at 128** →
    dynamic-seq-length is impossible without re-exporting the model (out of scope).
  - Batched inference (N=16, pad to 128): **1.22x** speedup, results **bit-identical**
    (6/6 terms, max score delta 0.0) — candidate for a follow-up PR.
- **Stale plan items found:** the "early exact-match exit before ONNX inference" plan item
  ALREADY exists (`MeSHMatcher.cs` `_meshNameLookup` check before `ComputeEmbedding`), and
  `mesh_terms.json` already includes entry terms/synonyms (e.g. "Fetal Anomalies" → CUIs), so
  exact-match coverage is already as good as it gets without new data files.
- **Root cause ranking:** per-term inference dominates; the 50k-entry `MeSHMatchCache` cap
  clears ~4x over the remaining corpus vocabulary (~150-250k unique terms), re-paying
  inference for every hot term each clear; `GetMeshDescriptorId` re-opens a fresh context on
  every CUI miss (misses were not cached).

### Changes (this branch)

1. **`MeSHMatchCache.MaxEntries` 50k → 250k** — holds the whole corpus vocabulary; each term
   pays inference exactly once (~30-40MB, under the 768m ingestion limit). Biggest safe win.
2. **`StudyRepository.GetMeshDescriptorId`** — cache `-1` misses so absent CUIs stop
   re-opening fresh contexts per occurrence.
3. **Batch phase timing logs** — `LogBatchPhaseTimes` (prefetch / map+match / save / post)
   via optional `ILogger<StudyRepository>` (wired in IngestionApp); confirms the match-vs-DB
   split on the next deploy and targets the follow-up batching PR.

### Tests

- `MeSHMatchCacheTests.Add_BeyondCap` updated for the new cap (250_001 + surviving entry).
- Full Release suite: 643/643 (Scrapers.Tests 399, DataApi.Tests 99, Integration 38,
  Frontend.Tests 107), 0 warnings, `dotnet build -c Release` 0 errors.

### Not done (candidate follow-up PRs, in order of value)

- **Batch ONNX inference** in a repository-level `MatchBatch` path (N=16, verified bit-identical,
  ~1.2-1.5x on the match phase) — deferred because it requires restructuring the
  data-loss-critical mapping loop; the new timing logs will confirm it's worth it.
- SIMD cosine scan (~10-25% on the droplet, memory-bandwidth-bound) — float-ordering changes
  make results not bit-identical; only if A/B tolerance is acceptable.
- Splitting openpayments/enrichment out of the ingestion container (OOM headroom, issue #364 watch item).

---

## Attempt log — Batched ONNX inference for MeSH matching (feature/batch-mesh-inference)

**Date:** 2026-08-08

### Why (post-#434 production data)

- Production phase logs (deployed #434, 6 batches since 13:36Z restart) show **map+match is
  98-99% of every batch**: batch 1 cold 993,546ms, batches 2-6 flat 456-598s; `save` 0ms,
  `prefetch` 1-4s. DB and CT.gov fetch are NOT the bottleneck.
- Flat batch times ≈ 170-200ms/term × ~3,000 unique terms per 200-record batch = the
  single-shot ONNX rate; the 250k cache was only ~6% covered after 6 batches, so it cannot
  help until later in the sweep (each term pays inference exactly once per process).
- Memory theory rejected: `OOMKilled=false`, `Restarts=0`, 42% CPU at snapshot — the
  container is not memory-bound and not CPU-saturated between batches.

### Change

`MeSHMatcher.MatchBatch` rewritten from a serial stub (`Select(Match)`) to a true batched
implementation:

1. Dedupe terms; resolve memo-cache and exact-name hits without inference.
2. Remaining unique terms: one `session.Run` per chunk of N=16 (`-1x128` batch dim is
   dynamic) instead of one `session.Run` per term; per-row mean-pool + L2 normalize mirrors
   `ComputeEmbedding` math exactly.
3. Shared `FindBestMatch` (cosine scan) + `BuildResult` helpers used by both `Match` and
   `MatchBatch` so single/batch paths cannot diverge.
4. `StudyRepository` map loop: keywords, conditions, interventions now collect the record's
   terms into one `MatchBatch` call (indexed consumption, identical filter semantics).
   No entity/data-loss logic changed.

### Tests

- `MeSHMatcherBatchTests` (4 new, DB-free): batch vs serial **bit-identical** on real CT.gov
  fixture terms (Value/SideAValid/SideBMatched/MeshTerm/MeshCui/Category/Similarity),
  duplicate terms, empty input, threshold-boundary agreement.
- Full Release suite: 647/647 (Scrapers.Tests 403, DataApi.Tests 99, Integration 38,
  Frontend.Tests 107), 0 warnings, 0 errors.

### Not done

- Persistent (Postgres-backed) match cache — keeps per-term inference once-EVER across
  restarts and speeds the hourly incremental sweep; deferred (user chose batch-only PR).
- Droplet resize to 2 vCPU/4GB — user declined for now; ONNX scales ~linearly with cores.
- SIMD cosine scan — float-order drift, not bit-identical; rejected.

---

# Manual Run Trigger (Issue #356 part 2, P1 of MVP framework)

## Status: shipped (PR #438)

### Problem

No safe way to force a scrape or a full re-sync without dropping the DB. The only re-run
path was `ResetDatabaseAsync` (destructive). MVP P1 requires: `POST /api/ingest/run` with
`mode=incremental|full`, and a button on the Status page.

### Design

- `data_source_state` gains a nullable `manual_run_mode` column (null = none).
- `POST /api/ingest/run?mode=incremental|full` (DataApi, PipelineEndpoints) validates mode
  via pure `ManualRunRequest.IsValidMode` and writes the flag through
  `IDataSourceStateService.RequestManualRunAsync` — DataApi never talks to the scraper
  directly (single gateway rule; the flag lives in the shared DB).
- `ClinicalTrialsScrapeService` loop consumes the flag every 10s tick
  (`ConsumeManualRunAsync`): `incremental` → set `_lastRunTime = MinValue` so the next
  interval check fires immediately; `full` → same + `_forceFullSweep = true`.
- `ManageBackfillAsync` honors `_forceFullSweep` by replanning and enqueueing EVERY date
  window, ignoring completed-window coverage (`PlanAndEnqueueSweepAsync(forceFull: true)`),
  then marks in-progress; the existing chunked engine (10h timeouts, dead-letter retry,
  idempotent upsert) does the re-fetch. A sweep already in flight consumes the request
  (it already covers the whole corpus).
- Status.razor: "Run incremental now" + "Full re-sync" buttons (disabled while a sweep is
  in-progress).

### Why not literal "clear LastSyncTimestamp" (spec wording)

The spec says `mode=full` clears `LastSyncTimestamp`, but with a cleared cursor the next
discovery counts the whole corpus (~597k) and enqueues ONE giant `studies.discovered`
event that exceeds the claim timeout and restarts from scratch — the exact legacy
pathology the chunked backfill engine was built to replace. Forcing a fresh chunked sweep
achieves the same outcome ("full re-sync, no DB drop", the #356 acceptance criterion) with
the proven resumable machinery.

### Tests

- `ManualRunRequestTests` (11 DB-free cases: case-insensitive valid modes, rejects
  null/empty/bogus).
- Schema guard `DataSourceStateTable_HasBackfillColumns` extended with `manual_run_mode`
  (no new DB test — schema-guard layer).
- `StatusPageFullResyncButtonPostsIngestRunRequest` bUnit: renders buttons, clicking
  "Full re-sync" POSTs `?mode=full`.
- Full Release suite: 659/659 (Scrapers.Tests 414, DataApi.Tests 99, Integration 38,
  Frontend.Tests 108), 0 warnings, 0 errors.

### Gotchas

- `Results.Accepted(...)` needs a URI — used `Results.Json(statusCode: 202)` instead.
- Razor attribute nested double-quote strings must use single-quoted attributes.
- EF columns are snake_case via explicit `HasColumnName` — forgot initially, migration
  generated `ManualRunMode`; reverted and regenerated with `manual_run_mode`.

---

# Bulk DLQ Retry (Issue #436)

## Status: shipped (PR #439)

### Problem

Watchdog #436: `deadLetterCount=5454` (5423 `investigator.enrichment`, 11
`studies.discovered`, 18 `medicare.utilization`) from enrichment NPI collisions. Only
per-event retry existed — no way to recover en masse.

### Change

- `EventQueueService.RetryAllDeadLetterEventsAsync(eventType?, limit, cancellationToken)`
  re-enqueues dead-lettered events in claim-timeout-safe batches.
- `POST /api/event-queue/dead-letter/retry-all?eventType=` (EventQueueEndpoints).
- Integration coverage extended in `BackfillLifecycleTests` (event enqueue → process fail →
  dead-letter → retry-all → re-enqueued), fakes updated.

### Not done (user action, no droplet access)

- The actual retry on production: `curl -X POST
  http://localhost:5003/api/event-queue/dead-letter/retry-all?eventType=investigator.enrichment`
  then `?eventType=studies.discovered`, verify #436 clears.

---

# A/B Single-Matcher Decision Read Path (Issue #356 part 5, P4 step ⑤)

## Status: shipped (PR #440, deployed 2026-08-08 run 31267516282)

### Problem

`rejected_terms` + rule-vs-BERT A/B results exist (A = rule-based, B = BERT) but there is
no read path to quantify agreement — the single-matcher decision cannot be made. Data loss
remediation is complete (all 7 priorities FIXED), so remaining MVP work is this + P2 #313
+ P7 domain.

### Change

- `StudyRepository.GetRejectedTermsSummaryAsync(source?)` + `GetRejectedTermsPagedAsync
  (source?, disagreementOnly?, page, pageSize)`. Summary computed by pure, DB-free
  `RejectedTermsSummary.Compute(IReadOnlyCollection<RejectedTermRow>)`; `RejectedTermRow`
  is a top-level `readonly record struct` (CA1034 forbids nested types).
- EF projection `Select(r => new RejectedTermRow(...))` — constructor-parameter
  projections are supported client-side; no translation risk.
- `GET /api/rejected-terms/summary` + `GET /api/rejected-terms?disagreementOnly=&page=`
  (DataApi/Endpoints/PipelineEndpoints.cs).
- DataQuality.razor tab 4 "Matcher A/B": agreement %, total/disagreements/accepted/
  rejected cards, similarity bands, disagreement samples (`?disagreementOnly=true&
  pageSize=50`).

### Tests

- `RejectedTermsSummaryTests` (4 DB-free): empty input, agree/disagree counts + ratio,
  band boundaries, accepted flag honored.
- `Tab4ShowsMatcherAgreementAndDisagreementSamples` bUnit (mock summary + samples).
  Gotchas: the tab heading renders in the loading state too — `WaitForState` must await the
  sample data, not the heading; `P1` percent format emits "92.0 %" with a space, assert on
  the number only. Existing `Tab0IsActiveByDefault` link-count assertion bumped 4 → 5.
- Full Release suite: 664/664 (Scrapers.Tests 418, DataApi.Tests 99, Integration 38,
  Frontend.Tests 109), 0 warnings, 0 errors.

---

# Scraper Progress Today card (Status page)

## Status: shipped (PR #442, deployed 2026-08-09 run 31332786292)

### Problem

Status page showed total sweep progress but nothing about recent ingest velocity. The
user wants a "Scraper Progress Today" card next to the full progress showing rows added
in the last day.

### Decisions (user)

- **Window:** rolling last 24h UTC (`created_at >= nowUtc - 24h`), not calendar day.
- **Placement:** new card in its own row below the Scraper Progress / Database row.
- **Metric:** new studies first persisted (`studies.created_at`). It is stamped only at
  first insert (StudyRepository.cs:162) and never touched by upsert (`MapRecordToEntity`),
  so hourly incremental runs, full re-syncs, and DLQ re-processes cannot double-count.
  Child tables are cleared/re-added per upsert and have no timestamps — not countable.
- **No reset-db in this PR** — index-only migration, deploy with `reset_db=false`.

### Change

- Migration `AddStudiesCreatedAtIndex` — `IX_studies_created_at` (index only, no data).
- `StudyRepository.CountStudiesAddedSinceAsync(DateTime fromUtc)`.
- Pure helper `Scrapers/Utilities/IngestProgressWindow.Last24Hours(nowUtc)` (DB-free,
  unit-tested; same pattern as `PageViewStatsAggregator`).
- `/api/scraper-progress` extended with `addedLast24h` + `sinceUtc` (same 5-min cache).
- Status.razor: "Scraper Progress Today" card (count + window start), DTO extended.

### Tests

- `IngestProgressWindowTests` (3 DB-free): fixed now → expected boundary, day-boundary
  cross, subsecond precision preserved.
- `StatusPageShowsScraperProgressTodayCard` bUnit; `MockDefaultsWithStats` scraper-progress
  mock now returns full payload (was empty object).
- Schema guard `StudiesTable_HasQueryIndexes` extended with `IX_studies_created_at`.
- `StudyRepositoryHappyPathTests` upsert test extended: count=1 in 24h window after
  insert, 0 outside; re-upsert must NOT move created_at.
- Full Release suite: 668/668 (Scrapers.Tests 421, DataApi.Tests 99, Integration 38,
  Frontend.Tests 110), 0 warnings, 0 errors.

---

# Remove Side A — single matcher (BERT) gates keyword acceptance (P4 step ⑤, issue #356 part 5)

## Status: shipped (PR #444, deployed 2026-08-09 run 31335560370)

### Why

The framework's P4 step ⑤ decision: keep BERT only, delete Side A. The A/B read path
(PR #440) already proved the gate was BERT-only — `MeSHMatchResult.Accepted =>
SideBMatched` — so Side A (`IsValidConditionSimple` rule + `side_a_valid` column) was
pure dead weight recorded alongside every evaluation.

### Change

- Deleted `IsValidConditionSimple` + `s_icdRegex` (MeSHMatcher), `SideAValid` from
  `MeSHMatchResult` / `RejectedTermEntity` / context mapping / persist block.
- `RejectedTermsSummary` slimmed to (Total, Accepted, Rejected, SimilarityBands);
  `RejectedTermRow` to (SideBMatched, SideBSimilarity, Accepted); dropped
  `disagreementOnly` from `GetRejectedTermsPagedAsync` and the API.
- `/api/rejected-terms` and `/summary` no longer expose `sideAValid`/`agreement`/
  `disagreements`. DataQuality tab 4 is now "Keyword Gate": BERT-only summary cards,
  similarity bands, recent evaluations (no Side A column).
- Migration `RemoveSideAMatcher` drops `side_a_valid` (data-loss warning expected and
  intentional — column served the A/B decision, which is complete).
- Removed dead `rejectedConditions` variable and the unreachable `RejectionReason`
  "unknown" branch.
- Docs: MVP_framework P4 marked done; scraper_architecture §7.7 updated.

### Tests

- `RejectedTermsSummaryTests` rewritten for BERT-only summary (4 tests: empty, counts,
  band boundaries, accepted-flag driving totals).
- `MeSHMatcherBatchTests` lost the SideAValid equality assertion (SideB fields still
  bit-identical between batch and serial).
- `Tab4ShowsKeywordGateSummaryAndSamples` bUnit (mock updated, no sideAValid).
- Full Release suite: 668/668, 0 warnings, 0 errors.

---

# MeSH cosine scan alias deduplication

## Status: implemented locally (`feature/mesh-ingest-performance`)

### Problem

The MeSH resource contains 61,794 aliases but only 31,110 unique CUI embeddings.
`FindBestMatch` scanned every alias and recalculated the same 768-value dot product
for aliases sharing an embedding. This was redundant work in both scalar and batched
matching paths.

### Change

- Build a constructor-time search order containing the first alias for each embedding.
- Scan unique embeddings only while preserving the original alias order, strict `>` tie
  handling, and therefore the previous winner and score arithmetic.
- Expose the candidate/alias counts internally for a real-resource regression guard.
- Do not repeat the previously rejected SIMD approach: its changed floating-point
  accumulation order was not bit-identical.

### Verification

- Unit test verifies duplicate removal and first-alias ordering.
- Real-resource integration test verifies the loaded matcher has fewer search candidates
  than aliases.
- Existing scalar/batch MeSH equivalence tests remain green.
- Controlled `MeshBench` run on the same machine: `origin/main` cold pass 100.52 ms/term;
  optimized branch 76.59 ms/term (approximately 23.8% lower). Warm cached passes remained
  effectively 0 ms/term.
- Full suite: 688 tests passed (Scrapers 437, DataApi 99, Integration 41, Frontend 111);
  Debug and Release builds passed with 0 warnings and 0 errors.

---

# ClinicalTrials.gov invalidated pagination recovery

## Status: implemented locally (`feature/retry-ctgov-pagination-reset`)

### Incident

- Deploy run [#31405292209](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31405292209)
  reported healthy containers and an active sync, but the post-deploy watchdog opened
  [issue #452](https://github.com/rakirs2/ClinicalTrialData/issues/452).
- Production had processed 0 records and 0 batches in the new run. The latest source error
  was ClinicalTrials.gov HTTP 400: "The data have probably changed while you were paginating."
- The event queue stats endpoint also exceeded its 30-second watchdog limit, but the recovery
  endpoint remained reachable and reported no dead-lettered study events.

### Change

- Detect the specific pagination-invalidated response instead of treating it as an ordinary
  permanent 400.
- Restart pagination from page one up to three times when no persistence callback has run.
- If a batch has already been emitted, rethrow to the existing event-level retry mechanism so
  persisted callbacks are never replayed within one client call.

### Verification

- Added fake-client tests for restart-before-first-batch, no callback replay, and bounded restart
  attempts.
- Debug and Release builds passed with 0 warnings and 0 errors.
- Debug and Release full suites passed: 691 tests each (Scrapers 440, DataApi 99,
  Integration 41, Frontend 111).

---

# Production legacy discovery stall recovery

## Investigation

**Date:** 2026-08-12

- Fresh watchdog run [#31612062885](https://github.com/rakirs2/ClinicalTrialData/actions/runs/31612062885)
  fetched current production payloads after the MiniLM deployment.
- The instance was healthy but still had one processing `studies.discovered`
  event with `eventId=1`, `total=596902`, `processed=400`, and an estimated
  completion of 2026-08-28. The current incremental progress was only about
  26.4 studies/minute.
- The event is a legacy count-only discovery payload. The existing recovery
  endpoint handled pending legacy events only, so this stale processing event
  continued to own the source sync and block bounded discovery.

## Fix

- Recover both pending and processing legacy count-only discovery events.
- Run legacy recovery once when `EventProcessingService` starts, before it
  claims new work. This is safe after deployment because the previous worker
  has stopped, and prevents the stale event from being claimed again.
- Clear in-flight progress fields when superseding the event.
- Extend the existing recovery integration test to cover a claimed processing
  legacy event.
- The first validation build caught CA1848 on the new startup log calls; those
  calls were converted to `LoggerMessage` delegates to match repository style.

## Verification

- `git diff --check` passed.
- `dotnet build ClinicalTrialData.slnx -c Release --no-restore` passed with
  0 warnings and 0 errors.
- Full tests passed: DataApi 99, Frontend 113, Scrapers 452, Integration 41;
  705 total.
- `bash scripts/test-health-check.sh` passed: 28/28 checks.

---

# Status page CT.gov live-study count

## Status: implemented locally (`feature/status-ctgov-live-count`)

### Problem

The Status page labeled `CountActiveStudiesAsync()` as "Studies (live, on CT.gov)".
That value is only the local row count excluding studies previously marked removed;
the CT.gov total already exists as `/api/scraper-progress.totalAvailable`.

### Change

- The live CT.gov row now renders the external `totalAvailable` count.
- Status requests now fail independently, so a slow event-queue telemetry request cannot
  prevent scraper-progress data from refreshing.

### Verification

- Added bUnit coverage for distinct local and CT.gov totals and queue-stat failure isolation.
- Debug and Release builds passed with 0 warnings and 0 errors.
- Debug and Release full suites passed: 693 tests each (Scrapers 440, DataApi 99,
  Integration 41, Frontend 113).

---

# System Status telemetry timeout reduction (issue #465)

## Status: implemented locally (`feature/system-status-telemetry-timeouts`)

### Problem

`/api/event-queue/stats` performed five full-table counts plus an unbounded historical
per-event-type average on every request. Under production event volume, watchdog and
Status page requests exceeded 30 seconds.

### Change

- Replace the five status counts with one grouped count query.
- Calculate per-type averages from the existing bounded recent-duration sample.
- Serialize concurrent stats requests through a short 15-second in-memory response cache.
- Show explicit unavailable states in the Status page when individual telemetry requests fail.

### Verification

- Extended existing integration coverage for grouped queue counts and the DataApi smoke path.
- Added frontend timeout and partial-response coverage.
- Debug and Release builds passed with 0 warnings and 0 errors.
- Debug and Release full suites passed: 699 tests each (Scrapers 445, DataApi 99,
  Integration 41, Frontend 113).

---

# Parallel event-queue telemetry reads

## Status: implemented locally (`feature/telemetry-parallel-query`)

### Problem

The first telemetry optimization still left `/api/event-queue/stats` waiting for the
queue summary query and event-type breakdown query serially. Production watchdogs
continued to observe timeouts after deployment.

### Change

- Start the independent queue summary and event-type breakdown queries together.
- Preserve the existing bounded queries and 15-second response cache.

### Verification

- Debug and Release builds passed with 0 warnings and 0 errors.
- Debug and Release full suites passed: 699 tests each.

---

# Background event-queue telemetry snapshot

## Status: implemented locally (`feature/background-telemetry-snapshot`)

### Problem

Request-path caching and parallel queries still allowed the first queue telemetry request
to block for more than the watchdog's 30-second budget.

### Change

- Refresh queue telemetry in a hosted background service with a 25-second database budget.
- Return the last snapshot immediately from `/api/event-queue/stats` with explicit
  `telemetryStatus`, timestamp, and error fields.
- Mark the initial response `warming` and failed refreshes `stale` instead of returning
  an unparseable timeout.
- Make the watchdog recognize non-healthy telemetry status.

### Verification

- DataApi smoke coverage asserts the telemetry status field.
- Script tests pass: 28/28.
- Release suites pass sequentially: DataApi 99, Scrapers 445, Integration 41,
  Frontend 113; 698 total.

---

# Long-running event progress heartbeat

## Status: implemented locally (`feature/long-running-event-heartbeat`)

### Problem

The ordinary event claim timeout was 30 minutes, but long-running ClinicalTrials.gov
ingestion events report progress between batches. The release query used only the
original `ClaimedAt`, so event 14054 was reclaimed while processing and restarted from
the beginning. Its processed studies were existing-row updates, while the pending
backfill never got a worker slot.

### Change

- Use `ProgressUpdatedAt` as the claim heartbeat for ordinary and backfill events, falling
  back to `ClaimedAt` before the first progress report.
- Clear stale progress fields on every new claim so a retry must establish a fresh heartbeat.
- Extend the existing integration test to prove a recent progress heartbeat prevents release
  and an old heartbeat still releases the claim.

---

# Prefetch ClinicalTrials.gov pages before slow persistence

## Status: implemented locally (`feature/prefetch-ctgov-pages`)

### Problem

Even with bounded pagination restarts and claim heartbeats, the client fetched the next
ClinicalTrials.gov page only after the current batch finished MeSH matching and persistence.
Production continued to receive invalidated page-token responses after 10+ minute batches.

### Change

- Fetch the next page immediately after receiving the current page and before invoking the
  slow batch callback.
- Hold one page in memory so the next token is not left idle during model processing.
- Preserve callback ordering and the existing no-replay behavior when a later page fails.

### Verification

- Added a client test proving the next request occurs before the first batch callback.
- Updated invalidation coverage for failures after a previously emitted batch.

---

# Bounded incremental ClinicalTrials.gov windows

## Status: implemented locally (`feature/bounded-incremental-windows`)

### Problem

The source cursor was 43 hours behind, creating one 2,089-study incremental event. Even
with page prefetching, the changing ClinicalTrials.gov result set invalidated the sequence
repeatedly before its first batch completed.

### Change

- Cap each incremental discovery event to a 24-hour window.
- Let successful events advance the cursor one bounded window at a time.
- Preserve inclusive boundary-day re-fetching because upserts are idempotent.

### Verification

- Added pure tests for no cursor, stale cursor, recent cursor, and invalid time ordering.

---

# MVP MeSH model benchmark

## Attempt log

**Date:** 2026-08-12

- Created isolated worktree `feature/mvp-mesh-model-benchmark` from `origin/main`.
- Clean application baseline passed `dotnet restore` and `dotnet build -c Release`
  with 0 warnings and 0 errors.
- First benchmark attempt used `dotnet run --no-restore` for the standalone
  `experiments/MeshBench` project. It failed because the project is not part of
  `ClinicalTrialData.slnx` and had no standalone `obj/project.assets.json`.
- Next attempt must restore `MeshBench` independently before running it.
- The first experiment solution build after the configurability changes failed
  on CA1859 for the token lookup helper; the parameter was changed to the
  concrete dictionary type used by the matcher.
- A subsequent `experiments/Experiments.slnx` build also surfaced unrelated
  pre-existing `MeshCompare` errors (`ConfigureNpgsql` and an obsolete
  `StudyConditionEntity.Condition` member), so the benchmark projects are
  validated directly instead of expanding this PR into a stale experiment fix.
- Direct benchmark compilation then failed because CLI argument arrays use
  `Length`, not `Count`; both sites were corrected.
- Targeted validation then hit a local NuGet cache inconsistency: restored
  assets referenced analyzer DLLs absent from the global package cache. The
  application baseline had passed before this cache issue appeared; force
  restore is required before repeating the tests.
- After the cache repair, the benchmark build reported CA1859 for the
  concrete `BatchSizes` property type; it was changed from
  `IReadOnlyList<int>` to `List<int>`.

## Results

- Current BioBERT baseline: synthetic 364-term cold pass was 40.99s serial and
  36.70s at batch 16; warm cache was approximately 0ms/term.
- Dynamic INT8 BioBERT: 33.96s at batch 16 in the measured run (7.5% faster);
  6 acceptance disagreements and 8 top-CUI disagreements versus baseline.
- DistilBERT Sentence-Transformer: 26.03s at batch 16 (29.1% faster). At an
  exploratory threshold of 0.55, condition F1 was 0.934 vs. BioBERT 0.915,
  but keyword recall was 0.849 vs. 0.856.
- MiniLM-L6-v2: 10.76s at batch 16 (70.7% faster). At an exploratory threshold
  of 0.55, condition F1 was 0.933 and keyword recall was 0.957.
- Initial benchmark decision was to defer the production switch pending review;
  the later MVP rollout below intentionally chooses MiniLM for throughput.
- Full solution validation first failed because `Markdig.dll` was missing from
  the local NuGet global package cache while building Frontend. `git diff
  --check` passed and all affected Scrapers/benchmark projects built before
  the Frontend project was reached; a no-cache restore is required.

## Verification

- `git diff --check` passed.
- `dotnet restore ClinicalTrialData.slnx --force-evaluate --no-cache` passed.
- `dotnet build ClinicalTrialData.slnx -c Release --no-restore` passed with
  0 warnings and 0 errors.
- Full solution tests passed: DataApi.Tests 99, Frontend.Tests 113,
  Scrapers.Tests 451, Scrapers.IntegrationTests 41; 704 total.
- `dotnet build experiments/MeshBench/MeshBench.csproj -c Release`
  passed with 0 warnings and 0 errors.

## MVP rollout

- User selected the practical MVP tradeoff: good-enough matching quality is
  preferred over perfect BioBERT mapping agreement when BioBERT is the
  ingestion bottleneck.
- Promoted MiniLM-L6-v2 into `Scrapers/Resources/mesh` with 384-dimensional
  embeddings, uncased tokenizer metadata, and `matcher_config.json` threshold
  0.55. Removed the unused stale BioBERT `tokenizer.model` artifact.
- Updated the acronym persistence assertion from 5 to 6 accepted evaluations;
  all persisted keyword rows and evaluation rows remain correct.
- Final active-bundle benchmark: 8.58s batch-16 cold vs. 36.70s BioBERT
  baseline (76.6% faster); condition precision 0.921, recall 0.945, F1 0.933.
- Final local verification rerun: Release build passed with 0 warnings and 0
  errors; full suite passed with DataApi.Tests 99, Frontend.Tests 113,
  Scrapers.Tests 451, and Scrapers.IntegrationTests 41 (704 total). The active
  bundle measured 9.43s batch-16 cold, 10.27s serial cold, and condition F1
  0.933.
- Production-only cleanup completed: active exporter/configuration, cache
  normalization, UI, architecture docs, and startup detection now target
  MiniLM; the unrelated DistilBERT classifier experiment and historical BERT
  attempt logs remain preserved. The final cleanup verification passed the
  full Release suite (704 tests) and measured 8.74s batch-16 cold with
  condition precision 0.921, recall 0.945, and F1 0.933.

---

# Status page load parallelism

## Attempt log

**Date:** 2026-08-12

- Created `feature/status-page-load-parallelism` from merged `origin/main`.
- The first validation command ran from the parent worktree after creating the
  new worktree, so the new worktree had no assets files. The correction is to
  run restore/build with the new worktree as the working directory.

---

# P7 Caddy public-domain deployment

## Decision

- Use Caddy as a Docker Compose service instead of installing a host-level
  package. This follows the repository's Docker deployment standard and keeps
  TLS state in persistent Docker volumes.
- Caddy serves `clinaxis.org` and `www.clinaxis.org`, proxies `/api/*` to
  DataApi on port 5003, and proxies all other paths to the Frontend on port
  5001. This preserves Blazor Server SignalR on the same HTTPS origin.
- Watchdog checks use the public HTTPS routes so certificate, Caddy, Frontend,
  and DataApi failures are observable together.
