"""Feature encoding + audit for the NPI candidate training frame.

The export endpoint already emits numeric 0/1 feature columns (value + has_*
coverage flags, per the pi-features convention), so encoding here is:
  - coerce features and label to numeric
  - drop identity/meta columns
  - emit an audit report (rows, batches, positive rate, coverage per feature)

rule_score is deliberately NOT a model feature — labels derive from the rule's
own decisions (approved vs other candidates in a resolved batch), so a model
trained with rule_score as input would just learn to copy the rule.
"""

import os

import pandas as pd

import config


def load_frame(path: str | None = None) -> pd.DataFrame:
    if path is None:
        path = os.path.join(config.OUTPUT_DIR, "candidates.parquet")
    return pd.read_parquet(path)


def build_training_frame(candidates: pd.DataFrame) -> tuple[pd.DataFrame, dict]:
    frame = candidates.copy()
    for col in config.FEATURE_NAMES:
        frame[col] = pd.to_numeric(frame[col], errors="coerce").fillna(0.0).astype(float)
    frame[config.LABEL_COLUMN] = pd.to_numeric(frame[config.LABEL_COLUMN], errors="coerce").astype(int)

    has_cols = [c for c in config.FEATURE_NAMES if c.startswith("has_")]
    audit = {
        "rows": int(len(frame)),
        "batches": int(frame["person_id"].nunique()) if "person_id" in frame.columns else None,
        "positive_rate": float(frame[config.LABEL_COLUMN].mean()),
        "feature_coverage": {
            col: float(frame[col].mean())
            for col in has_cols
        },
        "rule_score_available": bool("rule_score" in frame.columns and frame["rule_score"].notna().any()),
    }
    return frame, audit


def save_frame(frame: pd.DataFrame, audit: dict) -> None:
    os.makedirs(config.OUTPUT_DIR, exist_ok=True)
    path = os.path.join(config.OUTPUT_DIR, "training_frame.parquet")
    frame.to_parquet(path, index=False)
    audit_path = os.path.join(config.OUTPUT_DIR, "corpus_audit.json")
    with open(audit_path, "w") as f:
        import json
        json.dump(audit, f, indent=2)
    print(f"wrote {path}")
    print(f"wrote {audit_path}")
