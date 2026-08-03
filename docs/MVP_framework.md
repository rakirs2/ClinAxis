# MVP Framework

Source of truth for the MVP scope and build order. Tracking issue: #382.

## Principles

- Design principles: minimal usage and minimal complexity (AGENTS.md §5 — small stack, no nginx, lean tests, tiny PRs).
- MVP core: search + study detail + investigator pages, plus the Investigator Recommendation Engine (#170).
- Any new feature that #379 would classify out of MVP is deferred.
- 1 feature = 1 PR (AGENTS.md §1); all changes pass the testing pyramid (§3).

## Pillars

### P1 — Completed scraper run

- **Definition of done:** `mode=full` re-sync completes end-to-end with 0 dead letters; second run is idempotent; Status + DataQuality pages green; backoff keeps failures visible.
- **Current state:** Pipeline exists (8 services) but prod is broken — CT.gov stuck "syncing" since Aug 1 (#364), 2,059 dead-lettered enrichment events (#336), ~19k backlog, scrape loop spins on failure (#377), re-ingest destructive/duplicating (#356).
- **PR order:** ① #377 backoff (pure helper) ② #336 duplicate-insert idempotency ③ #356 parts 1–2 (non-destructive upsert + manual trigger).

### P2 — Filter-based search on MeSH terms

- **Definition of done:** filter combos (condition/keyword/phase/status/location) with real-time results + shareable URL; false-reject rate measured; sub-500ms latency.
- **Current state:** `/search`, `/mesh-tree`, Search.razor exist; MeSH BERT matcher A/B + uncached (#335: 267K-row cosine scan/call); valid non-MeSH/acronym terms rejected (#343, #355); no multi-pivot filters (#27), no geo search (#30); prod 0-results bug (#313).
- **PR order:** ① #335 memo-cache ② #355 acronym expansion + gate + threshold ③ #27 filters ④ #30 geo search (after #380).

### P3 — PI name cleaning & disambiguation

- **Definition of done:** one identity per investigator (persons merged); rejects queryable + overridable from the instance; enrichment DLQ ≈ 0.
- **Current state:** NameFilter + NPPES/ORCID + NPI rule scorer (authoritative) + ONNX A/B; duplicates dead-letter (#336); `rejected_investigator_names` holds too many real names (#345); no queryable/iterable override (#344).
- **PR order:** ① #336 dup handling ② #345 cleanup workflow ③ #344 override API/UI.

### P4 — Keyword & location cleaning

- **Definition of done:** measured false-reject rate on keywords; acronyms resolve to descriptors; locations normalized + geo-searchable; per-field coverage on DataQuality page.
- **Current state:** KeywordFilter blocklist + 29-term short-list drops MI/CVA/DKA/PE/… (#343/#355); MeSH matches A/B-recorded but never gated; LocationMeshMatcher + `distinct-locations` exist; 22 CT.gov fields unpersisted (`docs/data_loss_remediation.md`).
- **PR order:** ① #355 acronym+gate (also serves P2) ② #380 location normalization ③ remaining data-loss fields in #356 re-sync.

### P5 — Additional data sources

- **Definition of done:** ≥1 source beyond CT.gov feeding a user-facing inference (already true) + one deliberate addition if it moves the rec engine.
- **Current state:** 6 sources beyond CT.gov (PubMed, NPPES, ORCID, Medicare, Open Payments, Semantic Scholar — #118). Pending: #221 PECOS, #222 QPP/MIPS, #223 FAERS. #379 audits MVP fit.
- **PR order:** ① close out #118 ② #223 FAERS research spike (decision gate: only build if it adds a rec-engine signal).

### P6 — Best-PI inference model

- **Definition of done (MVP):** rule-based ranked PI list for (therapy, condition, population) with explainable scores + simple UI form.
- **Current state:** #170 open; Phase 1 (rule-based scoring) defined, no code; missing signals: completion rate + enrollment velocity (#381), enrollment calc.
- **PR order:** ① #381 completion-rate + enrollment-velocity ② rule-based scorer (pure, unit-testable) ③ `POST /api/recommend/investigators` ④ UI form.

## Build order

**P1 → (P3, P4) → P2 → P5 → P6**

A reliable, clean pipeline (P1) is the dependency for every downstream pillar. #355 and #335 are small PRs that unlock search quality (P2/P4). P6 consumes everything else — it is last by design.

## Cross-cutting

- Re-evaluate after each pillar against #379 (architecture audit) and #382 (this roadmap).
- Attempts and failures logged in `.opencode/plans/PLAN.md` (AGENTS.md §11).
- GLOSSARY is human-maintained only.
