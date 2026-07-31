# PI Completion Model — v0 (experimental)

Predicts the probability that a principal investigator completes a future
clinical trial, trained on 2018–2019 studies from the local `ctgov-pg` mirror
(94K studies, 2000–2026). Exported as ONNX and served by DataApi's
`/api/investigator-finder` alongside the rule-based score.

## Scope

| Item | Value |
|------|-------|
| Question | "Which PI is best for these conditions?" → P(study completes) |
| Success label | `overall_status == "COMPLETED"` (vs `TERMINATED`) |
| Training window | studies starting 2018–2019 |
| Model | Logistic regression (StandardScaler + LR pipeline) |
| Features (4) | `priorStudyCount`, `priorCompletedCount`, `priorEnrollmentTotal`, `priorCompletionRate` — the PI's history strictly before the target study's start |
| Output | ONNX `model.onnx` + `feature_schema.json` → `Scrapers/Resources/pi-model/` |

## Serving parity

The four features mirror what `GetInvestigatorFinderCandidatesAsync`
(Scrapers/Persistence/StudyRepository.cs) returns per candidate:
`StudyCount`, `CompletedStudies`, `EnrollmentTotal`, `CompletionRate`.
At serving time there is no target study yet, so the candidate's full history
*is* the prior history. H-index / paper count are deliberately **not** model
features: `ctgov-pg` does not contain them, so they cannot be trained on; they
remain part of the rule-based score only.

## Identity resolution (v0)

PI identity = normalized name (`normalize_name` in `features.py`:
uppercase, strip titles/punctuation, collapse whitespace). No NPI/ORCID join.
This is a documented simplification — name collisions merge distinct PIs and
name variants split one PI. The cloud pipeline (later PR) uses NPI/ORCID.

## Usage

```bash
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt

# 1. Extract raw rows from ctgov-pg (env: CTGOV_DB_*)
.venv/bin/python extract.py

# 2. Build training frame + audit
.venv/bin/python - <<'EOF'
import features, extract
s, i = extract.extract()
frame, audit = features.build_training_frame(s, i)
frame.to_parquet("data/training_frame.parquet", index=False)
print(audit)
EOF

# 3. Train + export ONNX
.venv/bin/python train.py

# 4. End-to-end smoke test (synthetic data, no DB needed)
.venv/bin/python smoke_test.py
```

Artifacts are written to `Scrapers/Resources/pi-model/` (copied to output by
`Scrapers.csproj`). The audit report lands in `data/audit_report.json`.

## Performance (honest v0 numbers)

| Metric | Value |
|--------|-------|
| Training rows | 3,906 (2018–2019 PI studies, COMPLETED vs TERMINATED) |
| Base positive rate | 0.893 |
| Holdout AUC (all test rows) | 0.484 |
| Holdout AUC (PIs with prior history only) | 0.524 |
| Cold-start share (PIs with zero prior studies in mirror) | 0.90 |

The local mirror (`ctgov-pg`) holds 94K studies; most PIs appear once, so 90% of
window PIs have no prior history in this data and correctly collapse to the base
rate. The model only differentiates PIs with prior history — that is the correct
behavior for the sparsity problem, but there is simply not enough repeat-PI
history in the mirror for strong AUC. Expect materially better signal when
trained on the full production corpus (600K studies, 20 years) in the cloud
pipeline PR.

## Validation

The smoke test (`smoke_test.py`) runs the full pipeline on synthetic data and
asserts: training frame shape, learnability (AUC > 0.5), ONNX round-trip
(probabilities in [0,1], sum to 1), and monotonicity (higher prior completion
rate → higher P(completed)).

## Extensibility — adding data sources

### Endpoint-driven generation (no direct DB access)

All model training data comes from one DataApi endpoint,
`GET /api/export/training/pi-features`, which streams the training frame as CSV.
The window is **client-supplied**: required query params `from`/`to`
(yyyy-MM-dd, validated server-side — no defaults, no hardcoded training
window). Bounds: `from >= 2000-01-01` (prior-history data starts then) and
`to <= today`; anything else returns 400. The endpoint owns
feature computation (no-lookahead rules, coverage flags) and is the *only*
consumer-facing generator — no SQL, no mirror DB, no file handoff.

### The mechanism (for any future source, e.g., Medicare)

1. **Ingest into prod** — the source lands in the app DB via IngestionApp
   (NPPES/ORCID enrichment, CMS Medicare, CMS Open Payments, Semantic Scholar,
   PubMed are already there).
2. **Identity join** — the source must key on person id (directly, or via
   NPI/ORCID resolution — the v0 name-based identity is a documented
   simplification and cannot join NPI-keyed sources).
3. **As-of features** — every feature must be computable from data available
   before the study's start date (e.g., Medicare/Open Payments rows use
   `DataYear <= study start year`). Features that can't be rebuilt as-of are
   either omitted or exported with an explicit leak flag (see below).
4. **Coverage flags** — missing data is exported as explicit `has_*` columns
   (1/0), never silently imputed to 0.
5. **Schema + model versioning** — `feature_schema.json` records the exact
   feature list per model version; old artifacts remain loadable
   (schema-driven binding is a serve-side change-point, below).
6. **Ablate before adopting** — `source_ablation_experiment.py` compares the
   incumbent vs the new feature set on identical splits; a source is adopted
   only if it improves holdout AUC with documented coverage.

### Source catalog (already in the app DB)

| Source | Tables | Identity | Exported features (as-of) | Leak |
|--------|--------|----------|---------------------------|------|
| Trial history | studies, study_investigators | person | prior study count/completed/enrollment/rate (window start) | clean |
| PubMed | pubmed_papers, investigator_papers | person | papers before start, papers/year before start | clean |
| Medicare | medicare_utilizations (+ procedures) | person via NPI | beneficiaries, services, payments, risk score, has_medicare (year ≤ start) | clean |
| Open Payments (Sunshine Act) | open_payments | person via NPI | research/general payments, payor count, has_payments (year ≤ start) | clean |
| Semantic Scholar | investigator_metrics | person (name search) | **current** h-index, citations, i10, papers, has_metrics | **leaky — diagnostic only** |

### Leak-flagged features (h-index)

The stored Semantic Scholar h-index is a *point-in-time snapshot taken at
enrichment time* — for a 2018-19 training study it contains ~7 years of future
publications, some caused by the outcome itself (label leakage). It can't be
rebuilt as-of because per-paper citation histories are not stored. Policy:
the snapshot group is trained and reported in every ablation as a
**leaky upper-bound diagnostic**, but it is never a deploy candidate unless an
as-of rebuild (e.g., OpenAlex paper-level citations) becomes a source.
The `pubmed` group (papers before start) is the clean alternative.

### Serve-side change-points (future model-swap PR)

- `DataApi/Services/PiCompletionModel.Predict` hardcodes the four v0 features
  and tensor shape `[1,4]` — must become schema-driven (bind inputs by name
  from `feature_schema.json`).
- `InvestigatorFinderService`/`StudyRepository` candidates must expose the new
  feature values (same as-of semantics as the export endpoint).
- `feature_schema.json` needs `schemaVersion`/`modelVersion`; a model registry
  (`model_artifacts` table + upload endpoint) replaces SCP/file shipping so
  past models stay loadable and current-vs-past comparison is reproducible.

### Ablation results

Run 2026-07-31 against the prod instance
(`--base-url http://206.189.235.73:5003 --from 2018-01-01 --to 2019-12-31`):

| arm | rows | AUC | AUC(exp) | coverage |
|-----|------|-----|----------|----------|
| base | 760 | 0.5147 | n/a | 0.034 |
| base+medicare | 760 | 0.5147 | n/a | 0.034 |
| base+pubmed | 760 | 0.5147 | n/a | 0.034 |
| base+semach | 760 | 0.5147 | n/a | 0.034 |
| base+openpay | 760 | 0.5147 | n/a | 0.034 |
| base+all | 760 | 0.5147 | n/a | 0.034 |

Coverage: base 0.034, medicare 0.000, pubmed 0.001, semach 0.000, openpay 0.000.
AUC(exp) is undefined (too few experienced PIs in the holdout, single class).

**Null result — no source can show lift because no source has data.** Prod
enrichment (NPI/Medicare/Open Payments/Semantic Scholar/PubMed) runs at
ingestion time only via `investigator.enrichment` events; the 2018–19 cohort
predates the enrichment services and was never backfilled. Verified via
`/api/investigators`: 2018–19 PIs have `npi=null`, `medicare=null`,
`openPayments=null`, `paperCount=0`, while 2024–26 PIs are fully enriched. The
window can't move to recent years either: recent studies are mostly not
COMPLETED/TERMINATED, so labels vanish.

**Blocked on:** a re-enrichment backfill for existing persons (enqueue
`investigator.enrichment` for persons without NPI/metrics) — then rerun the
command above; the harness is ready and verified.

The script only ever calls a live instance (`--base-url` + required
`--from`/`--to`); it never reads local files.
