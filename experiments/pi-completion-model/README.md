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
