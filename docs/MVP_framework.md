# MVP Framework

Source of truth for the MVP scope and build order. Tracking issue: #382.

_Last synced with merged PRs through #423 (2026-08-07). P6 Phase 1 (rule-based) shipped — P1 code fixes are merged, but production recovery and a completed full run remain open. P7 public-domain routing is implemented pending production deployment._

## Principles

- Design principles: minimal usage and minimal complexity (AGENTS.md §5 — small stack, no nginx, lean tests, tiny PRs).
- MVP core: search + study detail + investigator pages, plus the Investigator Recommendation Engine (#170).
- Any new feature that #379 would classify out of MVP is deferred.
- 1 feature = 1 PR (AGENTS.md §1); all changes pass the testing pyramid (§3).

## Pillars

### P1 — Completed scraper run

- **Definition of done:** `mode=full` re-sync completes end-to-end with 0 dead letters; second run is idempotent; Status + DataQuality pages green; backoff keeps failures visible.
- **Current state:** Pipeline exists (8 services); CT.gov 400-stall root-caused + fixed (#376), watchdog alerts live (#363), backfill engine + lifecycle + throughput shipped (#400–#403); enrichment NPI-collision dead-letters fixed (#405 — prod DLQ retry pending ops follow-up); scraper-loop backoff landed (#422); non-destructive upsert landed (#423); open: production recovery, bounded incremental re-sync, the manual full-run trigger, and MeSH model throughput selection (#470).
- **PR order:** ① #377 backoff → landed (#422) ② #336 duplicate-insert idempotency → landed (#405) ③ #356 non-destructive upsert → landed (#423) ④ bounded incremental event/cursor recovery ⑤ manual full-run trigger ⑥ #470 MeSH model benchmark and selection.

### P2 — Filter-based search on MeSH terms

- **Definition of done:** filter combos (condition/keyword/phase/status/location) with real-time results + shareable URL; false-reject rate measured; sub-500ms latency.
- **Current state:** `/search`, `/mesh-tree`, Search.razor exist; MiniLM embedding matcher with memo-cache (#335 → #390, #470); MeSH gate + design allowlist live (#343 → #391); acronym expansion live (#355 → #398); MiniLM-L6-v2 embeddings with threshold 0.55 selected for MVP (#470); **#27 multi-pivot filters live** — real-time results + shareable URL (PR #408–#410); **#30 geo closed as region-based** — normalized country/state/city/facility + Z-MeSH hierarchy, MeSH-mode cascade fixed (PR #411); remaining: sub-500ms latency check (dev-measured on SnapshotDb; no flaky perf gate), false-reject-rate sampling owned by P4 ⑤; prod 0-results bug (#313).
- **PR order:** ① #335 memo-cache → landed (#390) ② #355 acronym expansion + gate + threshold → landed (#398, #391, #404) ③ #27 filters → landed (#408–#410) ④ #30 geo search → landed, closed as region-based (#411; proximity/map gated on a geocoding spike per `docs/scraper_architecture.md`).

### P3 — PI name cleaning & disambiguation

- **Definition of done:** one identity per investigator (persons merged); rejects queryable + overridable from the instance; enrichment DLQ ≈ 0.
- **Current state:** NameFilter false-rejection fixes live (#344 1/3 → #384); rejected names carry context + queryable API (#344 2/3 → #386); override review workflow live, ingest honors overrides (#344 3/3 → #388, harness sync → #389); duplicates dead-lettered + NPI-collision class fixed (#336 → #405); NPI rule scorer + ML A/B track shipped (#337, #339–#341).
- **PR order:** ① #336 dup handling → landed (#405) ② #345 cleanup workflow → landed (#389) ③ #344 override API/UI → landed (#384/#386/#388).

### P4 — Keyword & location cleaning

- **Definition of done:** measured false-reject rate on keywords; acronyms resolve to descriptors; locations normalized + geo-searchable; per-field coverage on DataQuality page; **a single MiniLM embedding matcher gates keyword acceptance — Side A removed**.
- **Current state:** MeSH gate + design-descriptor allowlist live (#343 → #391); acronyms expand to canonical terms before filtering (#355 → #398); locations normalized at ingest (#380 → #399); MiniLM-L6-v2 selected for MVP MeSH matching via #470 at threshold 0.55; data-loss remediation complete; **single-matcher decision landed — Side A removed** (PR #444: `side_a_valid` + `IsValidConditionSimple` deleted, embedding-only read path, Keyword Gate tab on DataQuality); changed MeSH mappings are accepted as an MVP throughput tradeoff and remain observable through DataQuality; 22 CT.gov fields unpersisted (`docs/data_loss_remediation.md`).
- **PR order:** ① #355 acronym+gate (also serves P2) → landed (#398, #391) ② #380 location normalization → landed (#399) ③ remaining data-loss fields in #356 re-sync ④ #470 MiniLM model replacement and threshold 0.55 ⑤ **Single-matcher decision:** read-only `/api/rejected-terms` → sample prod buckets (similarity bands, accepted) → labeled review → keep the MiniLM matcher, delete Side A (`side_a_valid` + `IsValidConditionSimple`).

### P5 — Additional data sources

- **Definition of done:** ≥1 source beyond CT.gov feeding a user-facing inference (already true) + one deliberate addition if it moves the rec engine.
- **Current state:** 6 sources beyond CT.gov (PubMed, NPPES, ORCID, Medicare, Open Payments, Semantic Scholar — #118). Pending: #221 PECOS, #222 QPP/MIPS, #223 FAERS. #379 audits MVP fit.
- **PR order:** ① close out #118 ② #223 FAERS research spike (decision gate: only build if it adds a rec-engine signal).

### P6 — Best-PI inference model

- **Definition of done (MVP):** rule-based ranked PI list for (therapy, condition, population) with explainable scores + simple UI form.
- **Current state:** **Phase 1 (rule-based) shipped** — completion/velocity signals (#381 → #414), pure explainable scorer (#415), `POST /api/recommend/investigators` (#416), `/recommend-investigators` UI form (#417). Known v1 limits: population input accepted but contributes no signal (needs eligibility parsing, phase-2); broad regions (EU) and proximity geo deferred; network factor not yet fed.
- **PR order:** ① #381 → landed (#414) ② rule-based scorer → landed (#415) ③ `POST /api/recommend/investigators` → landed (#416) ④ UI form → landed (#417). Phase 2 (ML ranking on historical outcomes) is out of MVP scope.

### P7 — Public domain deployment

- **Definition of done:** purchased domain resolves to the droplet; `https://<domain>` serves the Frontend; TLS is valid and auto-renewing; Blazor Server SignalR, cookies, and DataApi calls work without mixed-content errors; deploy and watchdog health checks pass.
- **Current state:** `clinaxis.org` and `www.clinaxis.org` resolve to the droplet; Caddy provides automatic TLS and reverse-proxy routing; deploy and watchdog checks validate the public HTTPS endpoints. Production HTTPS validation remains a post-merge deployment step.
- **PR order:** ① #346 domain/DNS → complete ② Caddy TLS approach and edge routing ③ update forwarded scheme, deploy, and watchdog settings ④ validate HTTPS, SignalR, cookies, API calls, and rollback.

## Build order

**P1 → (P3, P4) → P2 → P5 → P6 → P7**

A reliable, clean pipeline (P1) is the dependency for every downstream pillar. #355 and #335 are small PRs that unlock search quality (P2/P4). P6 consumes everything else, and P7 is the final public-release gate.

## Cross-cutting

- Re-evaluate after each pillar against #379 (architecture audit) and #382 (this roadmap).
- Attempts and failures logged in `.opencode/plans/PLAN.md` (AGENTS.md §11).
- GLOSSARY is human-maintained only.
