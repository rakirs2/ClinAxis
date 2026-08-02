# NPI Disambiguation Model — v0 (experimental, ML track)

Logistic-regression scorer for NPI candidate disambiguation, the ML track
follow-up to the precision-first rule scorer (PR #337, issue #162). One model
score per NPPES candidate; the batch's highest-scoring candidate is the
predicted winner. Served **in parallel** with the rule scorer (rule decision
stays authoritative) and recorded as `model_score` per candidate for A/B.

## Scope

| Item | Value |
|------|-------|
| Question | "Which NPPES candidate is this investigator?" → P(candidate is the right one) |
| Label | 1 = auto-approved candidate, 0 = other candidates in the same resolved NPPES batch (`person_identifier_candidates` + `NpiEnrichmentResult = "assigned"`) |
| Corpus | `GET /api/export/training/npi-candidates` — endpoint-driven, no DB access |
| Features (21) | 12 match features from `NpiFeatureExtractor` as value + `has_*` coverage flags (tri-state: missing ≠ false); `deactivated`, `orcid_match` are plain booleans |
| Excluded | `rule_score` (labels derive from rule decisions — a model trained on it would just copy the rule), identity columns, `batch_size` |
| Model | Logistic regression (StandardScaler + LR pipeline), exported ONNX → `Scrapers/Resources/npi-model/` |
| Serving | Parallel A/B recording via `model_score` (separate PR); rule remains authoritative until A/B shows lift |

## Labels — honest framing of the decision gate

The corpus labels come from the **rule's own decisions** (approved vs other
candidates in a resolved batch), so the rule achieves 100% precision on this
corpus by construction. "Model beats rule precision" is therefore **not
measurable on this corpus**. What IS measured:

1. **Learnability** — holdout AUC clearly above 0.5 and batch-winner accuracy
   above random selection (1 / mean batch size). A model that cannot learn the
   rule's implicit patterns is useless.
2. **Ambiguity-resolution lift (the real deploy gate)** — share of batches the
   rule left `ambiguous` that the model resolves with high confidence, and the
   precision of those resolutions. Measured after the parallel-serving PR runs
   on prod (requeue-ambiguous + model-score corpus); documented there.

If the model adds no lift beyond the rule, the null result is recorded in
`.opencode/plans/PLAN.md` and the rule stays — same policy as the PI
completion model's ablation (see `experiments/pi-completion-model/README.md`).

## Usage

```bash
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt

# 1. Download the corpus from a live DataApi instance (required from/to window,
#    filters candidate created_at)
.venv/bin/python extract.py --base-url http://localhost:5003 \
    --from 2000-01-01 --to $(date +%F)

# 2. Build the training frame + corpus audit
.venv/bin/python - <<'EOF'
import features
frame, audit = features.build_training_frame(features.load_frame())
features.save_frame(frame, audit)
print(audit)
EOF

# 3. Train + export ONNX + audit report
.venv/bin/python train.py

# 4. End-to-end smoke test (synthetic data, no DB/API needed)
.venv/bin/python smoke_test.py
```

Artifacts are written to `Scrapers/Resources/npi-model/` (`model.onnx` is
Git-LFS tracked, `feature_schema.json` documents the exact feature order and
metrics — the serve-side binding contract).

## Serving parity

At serve time (InvestigatorEnrichmentService) the features are computed fresh
by `NpiFeatureExtractor.Extract` for every candidate — the same pure function
the export endpoint uses, so the training frame and the serving input have
identical semantics. The ONNX input tensor binds by name/order from
`feature_schema.json`.

## Performance (v0 — to be filled after first training run against prod)

| Metric | Value |
|--------|-------|
| Corpus rows / batches | TBD |
| Positive rate | TBD |
| Holdout AUC | TBD |
| Batch-winner accuracy | TBD |
| Precision @ assign threshold | TBD |

## Validation

`smoke_test.py` runs the full pipeline on synthetic resolved batches and
asserts: learnability (AUC > 0.6, batch-winner accuracy > 0.6), ONNX
round-trip (output shape, probabilities in [0,1]), and schema-driven input
binding.
