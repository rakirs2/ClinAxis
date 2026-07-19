# Data Problems & Experiment Artifacts

## Problem 1: Messy CT.gov Conditions

### The Problem

ClinicalTrials.gov stores study conditions as free-text strings. These are frequently:
- Mixed with ICD-10 codes (`"I10"`, `"J45.0"`)
- Inconsistently formatted (abbreviations, typos, punctuation)
- Excessively specific sub-terms that don't generalize across studies
- A mix of disease and non-disease entries (procedures, devices, symptoms)

Parsing this into clean, structured condition data is non-trivial.

### The Experiment

Since most clinical studies link PubMed references (via PIs, required citations, etc.), and PubMed has **MeSH (Medical Subject Headings)** — a controlled, curated vocabulary maintained by NLM — the question was:

> *Can we skip parsing messy CT.gov conditions entirely and instead pull clean, disease-classified MeSH terms from linked PubMed references?*

### Method

1. Sampled **100 studies** from the database that have both conditions and PubMed references.
2. Fetched full PubMed XML for each PMID (via NCBI E-utilities POST).
3. Parsed `<MeshHeadingList>` to extract all MeSH descriptors (+ qualifiers, + UI).
4. For each DescriptorUI, fetched tree numbers from `id.nlm.nih.gov/mesh/{UI}.json`.
5. Filtered to **disease-only** descriptors: tree numbers starting with `C` (Diseases) or `F03` (Mental Disorders).
6. Mapped disease MeSH to ICD-10 codes via a lookup table (~150 common mappings).

### Results

| Metric | Value |
|---|---|
| Studies sampled | 100 |
| Studies with any MeSH | ~75 |
| Studies with disease MeSH | 55 |
| Unique raw CT.gov conditions | 159 |
| Unique disease MeSH descriptors | 68 |
| Unique ICD-10 codes mapped | 16 |

### Key Takeaways

- **MeSH provides cleaner, more standardized condition data** than raw CT.gov text. 159 unique raw conditions collapsed to 68 standardized disease descriptors.
- **Coverage gap**: Only 55/100 studies had disease-classified MeSH terms. Causes: studies linking to non-disease PubMed articles, non-English articles without MeSH, or articles not yet indexed.
- **ICD-10 mapping works** but limited by lookup table size. An automated mapping via UMLS would be more scalable.

### Artifacts

- `data_problems/01_mesh_vs_raw_conditions.tsv` — All 100 studies with raw conditions vs unfiltered MeSH (tab-separated)
- `data_problems/02_mesh_filtered_disease_icd10.tsv` — Studies with disease-only MeSH + ICD-10 mappings (tab-separated)
- `experiments/MeshCompare/` — Self-contained experiment tool (references Scrapers via InternalsVisibleTo; not part of solution.sln)

### How to Re-run

```bash
docker compose up -d                          # ensure Postgres is running
dotnet run --project experiments/MeshCompare/  # rebuilds on first run
```
Output files are written to `data_problems/`.
