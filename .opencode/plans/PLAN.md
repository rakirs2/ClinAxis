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
