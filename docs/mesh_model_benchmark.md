# MeSH Model Benchmark

Issue: [#470](https://github.com/rakirs2/ClinicalTrialData/issues/470)

**Date:** 2026-08-12

## Scope

The benchmark measures the current C# `MeSHMatcher` on 364 distinct synthetic
condition-like terms. Cold measurements use a fresh matcher and therefore pay
for inference and the MeSH scan. Warm measurements reuse the matcher cache.
Quality checks use the locally captured condition and keyword label files.

The former BioBERT resource bundle is the baseline. MiniLM-L6-v2 is now the
selected MVP resource bundle; the decision favors throughput over exact
agreement with the previous embedding model.

## Performance

| Candidate | Batch | Cold total | Cold p50 | Cold p95 | Relative to baseline |
|---|---:|---:|---:|---:|---:|
| BioBERT baseline | 1 | 40.99 s | 107.32 ms | 216.32 ms | baseline |
| BioBERT baseline | 16 | 36.70 s | 97.26 ms | 132.43 ms | baseline |
| BioBERT dynamic INT8 | 16 | 33.96 s | 88.57 ms | 125.48 ms | 7.5% faster |
| DistilBERT SBERT | 1 | 26.76 s | 75.19 ms | 108.33 ms | 34.7% faster |
| DistilBERT SBERT | 16 | 26.03 s | 69.05 ms | 87.94 ms | 29.1% faster |
| MiniLM-L6-v2 | 1 | 9.63 s | 28.25 ms | 33.70 ms | 76.5% faster |
| MiniLM-L6-v2 | 16 | 10.76 s | 28.07 ms | 37.70 ms | 70.7% faster |

Warm-cache runs were approximately `0 ms/term` for every candidate because all
364 terms were cached after the cold pass.

## Labeled Quality

The condition label file contains both positive and negative examples. The
keyword file used here contains only positive examples, so its precision is not
informative; keyword recall is the useful metric for that file.

| Candidate | Threshold | Condition precision | Condition recall | Condition F1 | Keyword recall |
|---|---:|---:|---:|---:|---:|
| BioBERT baseline | 0.65 | 0.940 | 0.892 | 0.915 | 0.856 |
| DistilBERT SBERT | 0.55 | 0.918 | 0.951 | 0.934 | 0.849 |
| MiniLM-L6-v2 | 0.55 | 0.921 | 0.945 | 0.933 | 0.957 |

The thresholds above are exploratory. They are not approved production
thresholds.

## Candidate Findings

### INT8 BioBERT

Dynamic INT8 quantization is easy to deploy, but the measured speedup is well
below the 50% MVP target. On the 364-term comparison it changed acceptance for
6 terms and the top CUI for 8 terms. It is not the preferred next step.

### DistilBERT Sentence-Transformer

DistilBERT is materially faster than BioBERT and condition F1 is slightly
higher after threshold calibration. However, keyword recall did not improve,
and it does not meet the 50% speed target. It remains a valid fallback if
domain-specific quality is preferred over maximum throughput.

### MiniLM-L6-v2, Selected for MVP

MiniLM meets the speed target by a wide margin and produced the strongest
keyword recall in this local sample. It also preserved condition F1 relative to
BioBERT after lowering the threshold to `0.55`. Its generic model training and
changed MeSH top-CUI choices are accepted as an MVP tradeoff; the mapping drift
should be monitored through the DataQuality review path.

## Decision

Use MiniLM-L6-v2 for the MVP production resource bundle.

Follow-up work can include:

- Manual review of changed top-CUI mappings
- Evaluation against a labeled negative keyword set
- Acronym, case, generic-junk, and persistence regression tests
- Final threshold selection
- License and artifact review

If production quality proves unacceptable, revert the resource bundle to
BioBERT and pursue persistent term mapping or approximate nearest-neighbor
search instead.

## Reproduction

Build the isolated benchmark project and run the baseline benchmark:

```bash
dotnet restore experiments/MeshBench/MeshBench.csproj && dotnet run --project experiments/MeshBench/MeshBench.csproj -c Release -- --synthetic --batch-sizes 1,16 --passes 1 --output /tmp/mesh-baseline.json
```

The benchmark supports `--resources`, `--reference-resources`, `--labels`,
`--quality-only`, and `--output` for candidate comparison.
