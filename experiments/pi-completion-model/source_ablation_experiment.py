"""Multi-source feature ablation for the PI completion model.

Consumes the training frame exported by DataApi's endpoint
GET /api/export/training/pi-features (endpoint-driven: no direct DB access).

Trains the same logistic-regression pipeline (StandardScaler + LR, mirroring
train.py) on identical stratified train/test splits and compares feature sets:

  base      prior trial history (4 features)
  +medicare CMS Medicare utilization, as-of study start year
  +pubmed   papers published before study start
  +semach   Semantic Scholar CURRENT snapshot (leaky by design — diagnostic
            only, never a deploy candidate without an as-of rebuild)
  +openpay  CMS Open Payments (Sunshine Act), as-of study start year
  +all      every column group

For each model: holdout AUC on all rows and on PIs with prior history only,
plus per-source coverage (fraction of rows with has_* = 1). The leaky-vs-clean
h-index comparison (semach vs pubmed) quantifies how much of the snapshot's
lift is future information.

Usage:
  .venv/bin/python source_ablation_experiment.py \
      --url "https://<host>/api/export/training/pi-features?from=2018-01-01&to=2019-12-31"
  .venv/bin/python source_ablation_experiment.py --csv data/pi-features.csv
  .venv/bin/python source_ablation_experiment.py --csv data/pi-features.csv \
      --synthetic-signal medicare --strength 1.0
"""

import argparse
import json
import os

import numpy as np
import pandas as pd
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import roc_auc_score
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

import config

SOURCE_GROUPS = {
    "base": [
        "prior_study_count",
        "prior_completed_count",
        "prior_enrollment_total",
        "prior_completion_rate",
    ],
    "medicare": [
        "medicare_beneficiaries",
        "medicare_services",
        "medicare_payments",
        "medicare_risk_score",
        "has_medicare",
    ],
    "pubmed": [
        "papers_before_start",
        "papers_per_year_before_start",
    ],
    "semach": [
        "current_h_index",
        "citation_count",
        "i10_index",
        "total_papers",
        "has_metrics",
    ],
    "openpay": [
        "research_payments",
        "general_payments",
        "payor_count",
        "has_payments",
    ],
}

# Order of the ablation arms in the report.
ARMS = [
    ("base", ["base"]),
    ("base+medicare", ["base", "medicare"]),
    ("base+pubmed", ["base", "pubmed"]),
    ("base+semach", ["base", "semach"]),
    ("base+openpay", ["base", "openpay"]),
    ("base+all", ["base", "medicare", "pubmed", "semach", "openpay"]),
]

COVERAGE_FLAG = {
    "base": "prior_study_count",
    "medicare": "has_medicare",
    "pubmed": "papers_before_start",
    "semach": "has_metrics",
    "openpay": "has_payments",
}

LABEL_COLUMN = "label"


def load_frame(source: str) -> pd.DataFrame:
    """Load the export CSV from a URL or a local file."""
    if source.startswith("http"):
        return pd.read_csv(source)
    return pd.read_csv(source)


def coverage(frame: pd.DataFrame, group: str) -> float:
    col = COVERAGE_FLAG[group]
    if frame[col].dtype == bool:
        return float(frame[col].mean())
    return float((frame[col] > 0).mean())


def fit_arm(frame: pd.DataFrame, name: str, groups: list[str]) -> dict:
    features = [f for g in groups for f in SOURCE_GROUPS[g]]
    x = frame[features].to_numpy(dtype=np.float32)
    y = frame[LABEL_COLUMN].to_numpy()

    x_train, x_test, y_train, y_test, _, x_test_indexes = train_test_split(
        x, y, np.arange(len(frame)),
        test_size=config.TEST_FRACTION,
        random_state=config.RANDOM_SEED,
        stratify=y,
    )

    pipeline = Pipeline(
        [
            ("scaler", StandardScaler()),
            ("lr", LogisticRegression(max_iter=2000, random_state=config.RANDOM_SEED)),
        ]
    )
    pipeline.fit(x_train, y_train)

    y_proba = pipeline.predict_proba(x_test)[:, 1]
    auc = roc_auc_score(y_test, y_proba)

    experienced_mask = frame.iloc[x_test_indexes]["prior_study_count"] > 0
    experienced_auc = (
        roc_auc_score(y_test[experienced_mask], y_proba[experienced_mask])
        if experienced_mask.sum() > 1
        else None
    )

    return {
        "arm": name,
        "features": features,
        "rows": len(frame),
        "holdoutAUC": round(float(auc), 4),
        "experiencedHoldoutAUC": round(float(experienced_auc), 4) if experienced_auc is not None else None,
        "coefficients": {
            name: float(coef)
            for name, coef in zip(features, pipeline.named_steps["lr"].coef_[0])
        },
    }


def inject_synthetic_signal(frame: pd.DataFrame, group: str, strength: float) -> pd.DataFrame:
    """Post-hoc sanity mode: add a synthetic column correlated with the label
    into the given source group and confirm the harness detects it."""
    if group == "base":
        raise ValueError("synthetic signal must target a non-base group")
    rng = np.random.default_rng(config.RANDOM_SEED)
    latent = frame[LABEL_COLUMN].to_numpy(dtype=float)
    synthetic = latent * strength + rng.normal(0, 0.1, size=len(frame))
    frame[f"synth_{group}"] = np.maximum(0.0, synthetic)
    SOURCE_GROUPS[group] = SOURCE_GROUPS[group] + [f"synth_{group}"]
    return frame


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    source_group = parser.add_mutually_exclusive_group(required=True)
    source_group.add_argument("--url", help="full URL of the pi-features export endpoint")
    source_group.add_argument("--csv", help="path to an exported pi-features CSV")
    parser.add_argument(
        "--synthetic-signal",
        choices=[g for g in SOURCE_GROUPS if g != "base"],
        help="inject synthetic label-correlated signal into this group (harness sanity check)",
    )
    parser.add_argument("--strength", type=float, default=1.0, help="signal strength for --synthetic-signal")
    args = parser.parse_args()

    frame = load_frame(args.url if args.url else args.csv)

    required = [c for g in SOURCE_GROUPS for c in SOURCE_GROUPS[g]] + [LABEL_COLUMN, "study_nct_id", "person_id"]
    missing = [c for c in required if c not in frame.columns]
    if missing:
        raise SystemExit(f"export CSV is missing columns: {missing}")

    if args.synthetic_signal:
        frame = inject_synthetic_signal(frame, args.synthetic_signal, args.strength)

    print(f"rows: {len(frame)}, positive rate: {frame[LABEL_COLUMN].mean():.4f}")
    print(f"coverage: " + ", ".join(f"{g}={coverage(frame, g):.3f}" for g in SOURCE_GROUPS))
    print()

    results = {
        "rows": len(frame),
        "positiveRate": float(frame[LABEL_COLUMN].mean()),
        "coverage": {g: round(coverage(frame, g), 4) for g in SOURCE_GROUPS},
        "arms": [fit_arm(frame, name, groups) for name, groups in ARMS],
    }

    header = f"{'arm':<16} {'rows':>6} {'AUC':>7} {'AUC(exp)':>9} {'coverage':>9}"
    print(header)
    print("-" * len(header))
    for arm in results["arms"]:
        exp = "n/a" if arm["experiencedHoldoutAUC"] is None else f"{arm['experiencedHoldoutAUC']:.4f}"
        cov = max(results["coverage"].get(g, 0.0) for g in arm["arm"].split("+"))
        print(f"{arm['arm']:<16} {arm['rows']:>6} {arm['holdoutAUC']:>7.4f} {exp:>9} {cov:>9.3f}")

    os.makedirs(config.OUTPUT_DIR, exist_ok=True)
    report_path = os.path.join(config.OUTPUT_DIR, "ablation_report.json")
    with open(report_path, "w") as f:
        json.dump(results, f, indent=2)
    print(f"\nreport written to {report_path}")


if __name__ == "__main__":
    main()
