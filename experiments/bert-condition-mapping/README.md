# BERT Condition Mapping Experiment

Two parallel pipelines for normalizing ClinicalTrials.gov conditions to MeSH terminology.

## Pipelines Overview

| Pipeline | Approach | Purpose |
|----------|----------|---------|
| **MeSH** | Sentence-BERT + MeSH 2026 | Match conditions/keywords to standardized MeSH terms |
| **Classifier** | DistilBERT + canonical list | Validate conditions vs noise; normalize to curated list |

---

## MeSH Pipeline

Maps raw CT.gov conditions and keywords to MeSH 2026 descriptors using
`sentence-transformers/all-MiniLM-L6-v2` sentence embeddings with cosine
similarity. This is the production MVP embedding bundle.

### Steps

```
python run.py fetch    # Fetch 50 studies from clinicaltrials.gov API v2
python run.py mesh     # Download MeSH → match all conditions + keywords
python run.py report   # Generate per-study CSV + text report
python run.py all      # All three steps above
```

### How It Works

1. **fetch** — retrieves 50 studies, saves conditions + keywords as per-study CSVs
2. **mesh** — downloads MeSH desc2026.xml (298 MB, 31K descriptors, 62K terms with synonyms), builds MiniLM embeddings, matches each unique condition/keyword to the nearest MeSH term at ≥0.55 similarity
3. **report** — generates `mesh-per-study.csv` (long-format per study) with column `type` values:

| type | meaning |
|------|---------|
| `condition` | Original condition from CT.gov |
| `remapped_keyword` | Keyword that matched disease MeSH (promoted to condition) |
| `keyword` | Non-disease keyword |

### Key Logic

- **Category** determined by MeSH tree prefix: `C`/`F03` → disease, `E`/`G`/`H` → procedure, everything else → other
- **Threshold**: 0.55 (configurable via `MESH_THRESHOLD` in `config.py`)
- **Tree numbers** parsed from `<TreeNumberList>` in desc2026.xml (no separate mtrees file needed)

### Output Files

| File | Description |
|------|-------------|
| `data/mesh-mapping.csv` | All 243 rows with MeSH mapping |
| `data/mesh-per-study.csv` | Per-study long format, sorted by NCT ID then type |
| `data/mesh-report.txt` | Text report with stats, remapping details, per-study summary |

---

## Classifier + Normalization Pipeline

Fine-tunes a DistilBERT model to distinguish valid medical conditions from noise, then maps to a canonical list.

### Steps

```
python run.py download    # Pull 1,326 labeled conditions from DataApi
python run.py preseed     # Auto-label with heuristics (noise patterns, apostrophe diseases, canonical matches)
python run.py review      # Manual CLI review (y/n/s/q) to correct labels
python run.py train       # Fine-tune distilbert-base-uncased (3 epochs, early stopping)
python run.py evaluate    # Compare BERT vs rule-based IsValidCondition()
python run.py normalize   # Map to 263 canonical conditions via Sentence-BERT
python run.py export      # Generate mapping CSV + ONNX for C# inference
```

### Current Status

| Step | Status | Notes |
|------|--------|-------|
| `download` | Done | 1,326 rows from DataApi |
| `preseed` | Not run | Ready to auto-label |
| `review` | Not run | CLI tool needs human input |
| `train` | Not run | Model directory exists but is empty |
| `evaluate` | Not run | Requires trained model |
| `normalize` | Not run | Requires canonical list + model |
| `export` | Not run | Requires trained model |

---

## A/B Test

Compares the current `IsValidCondition()` approach (Side A) against MeSH matching (Side B).

```
python run.py ab-test
```

This:
1. Re-fetches 50 fresh studies from CT.gov
2. Runs `IsValidCondition()` on all conditions + keywords (Side A)
3. Runs MeSH matcher with ≥0.8 threshold (Side B)
4. Outputs comparison stats + blind review CSV

Output files:
- `data/ab-test-stats.txt` — side-by-side comparison
- `data/ab-test-review.csv` — blind review rows (rate each side manually)

### Evaluation Method

Sample ~50 rows from each side, rate "correct condition" or "not a condition" blind. Compare accuracy %.

**Winner determines next step:**
- MeSH wins → deprecate `IsValidCondition()` path
- IsValidCondition wins → keep both, MeSH as enrichment layer

---

## Data Files

| File | Rows | Content |
|------|------|---------|
| `data/training-conditions.csv` | 1,326 | Labeled conditions from DataApi |
| `data/canonical-conditions.csv` | 263 | Curated reference condition list |
| `data/ctgov-50-studies.csv` | 50 | Study metadata (NCT ID, title, status) |
| `data/ctgov-conditions.csv` | 104 | Raw conditions from 50 studies |
| `data/ctgov-keywords.csv` | 139 | Raw keywords from 50 studies |
| `data/mesh/desc2026.xml` | 298 MB | MeSH 2026 descriptors |
| `data/mesh/index.pkl` | — | 62K MeSH terms with categories |
| `data/mesh/embeddings.npy` | — | Sentence-BERT embeddings (62K × 384) |

---

## Config (`config.py`)

| Parameter | Default | Description |
|-----------|---------|-------------|
| `MESH_THRESHOLD` | 0.55 | Minimum cosine similarity for the MiniLM MeSH match (#470) |
| `BERT_MODEL_NAME` | distilbert-base-uncased | Classifier base model |
| `SENTENCE_BERT_MODEL` | sentence-transformers/all-MiniLM-L6-v2 | Production MeSH embedding model (#470) |
| `EPOCHS` | 3 | Classifier training epochs |
| `BATCH_SIZE` | 16 | Training batch size |
| `LEARNING_RATE` | 2e-5 | AdamW learning rate |
| `MAX_SEQ_LEN` | 64 | Tokenizer max length |
| `CLASSIFICATION_THRESHOLD` | 0.5 | Decision boundary for valid/invalid |

---

## Retraining

Both pipelines support retraining over time:

**MeSH pipeline:** Re-fetch more studies, or update `MESH_XML_URL` in `download_mesh.py` to a newer MeSH year.

**Classifier:** Add new training data (via `download_data.py`), re-preseed with updated heuristics, re-review edge cases, retrain.

---

## Dependencies

```
pip install -r requirements.txt
```

- torch, transformers, sentence-transformers
- pandas, numpy, scikit-learn
- tqdm, requests

---

## Human-Todos

Things that require human judgement:

- [ ] **Review preseed labels** — `python run.py review` walks through all auto-labeled conditions. Flag false positives/negatives manually before training.
- [ ] **Verify training data quality** — check `data/training-conditions.csv` for edge cases (ICD codes, generic trial terms like "treatment", test conditions)
- [ ] **Evaluate trained model** — after training, read `data/evaluation-report.txt`, inspect false positives/negatives, tune threshold if needed
- [ ] **A/B test blind review** — open `data/ab-test-review.csv` and rate each row: is Side A's output correct? Side B's?
- [ ] **Decide threshold strategy** — 84 terms below 0.8 threshold. Should the threshold be lowered? Fuzzy zone added?
- [ ] **Decide on keyword remapping** — 35 disease keywords found. Should all be promoted to conditions? Review edge cases.
- [ ] **Verify per-study output** — open `data/mesh-per-study.csv` in a spreadsheet. Does the keyword→disease remapping make sense for your domain?
- [ ] **Run full pipeline from scratch** — `python run.py all` to confirm everything works end-to-end after changes
- [ ] **Plan production integration** — decide: new DB table? ONNX sidecar? Replace `IsValidCondition()`?
