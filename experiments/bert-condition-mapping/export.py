import pandas as pd
import os
import sys
import torch
import numpy as np
from config import (
    CLASSIFIER_MODEL, MAPPING_TABLE, CANONICAL_CSV,
    MODEL_DIR, ONNX_MODEL, NORMALIZATION_MODEL, REVIEWED_CSV,
)
from train import predict_conditions
from normalize import normalize_conditions, load_canonical


def export_classifier_onnx():
    """Export trained classifier to ONNX for C# inference (like the removed BertNpiScorer)."""
    try:
        import torch.onnx as onnx
        from transformers import DistilBertTokenizer, DistilBertForSequenceClassification

        model_path = CLASSIFIER_MODEL
        if not os.path.exists(model_path):
            print(f"Model not found at {model_path}. Train first.")
            return False

        print("Exporting classifier to ONNX ...")
        tokenizer = DistilBertTokenizer.from_pretrained(model_path)
        model = DistilBertForSequenceClassification.from_pretrained(model_path)
        model.eval()

        dummy_input = tokenizer(
            "test condition", return_tensors="pt", padding="max_length",
            max_length=64, truncation=True,
        )

        os.makedirs(MODEL_DIR, exist_ok=True)
        torch.onnx.export(
            model,
            (dummy_input["input_ids"], dummy_input["attention_mask"]),
            ONNX_MODEL,
            input_names=["input_ids", "attention_mask"],
            output_names=["logits"],
            dynamic_axes={
                "input_ids": {0: "batch_size"},
                "attention_mask": {0: "batch_size"},
                "logits": {0: "batch_size"},
            },
            opset_version=14,
        )
        print(f"  ONNX model saved to {ONNX_MODEL}")
        return True
    except ImportError:
        print("  ONNX export not available (install torch.onnx deps). Skipping.")
        return False


def export_full_mapping(raw_csv: str | None = None):
    """Generate full mapping table from all available training data."""
    print("Generating full condition mapping table ...")

    canonical = load_canonical(CANONICAL_CSV)

    if raw_csv and os.path.exists(raw_csv):
        raw = pd.read_csv(raw_csv)
    elif os.path.exists(REVIEWED_CSV):
        raw = pd.read_csv(REVIEWED_CSV)
    else:
        print("  No training data found. Using test sample.")
        raw = pd.DataFrame({"value": [
            "Diabetes", "Heart Attack", "High Blood Pressure",
            "Cancer", "Concussion", "Inflammation",
        ]})

    conditions = raw["value"].unique().tolist()
    print(f"  Processing {len(conditions)} unique conditions ...")

    results = normalize_conditions(conditions, canonical)

    mapping_df = pd.DataFrame([{
        "raw_condition": r["raw_condition"],
        "canonical_condition": r["canonical_condition"],
        "similarity_score": r["similarity_score"],
        "is_new": r["is_new"],
        "alternatives": "; ".join(
            f"{a['condition']}({a['score']:.3f})" for a in r["alternatives"]
        ),
    } for r in results])

    mapping_df.to_csv(MAPPING_TABLE, index=False)
    print(f"  Mapping table ({len(mapping_df)} rows) saved to {MAPPING_TABLE}")

    unmapped = mapping_df[mapping_df["is_new"]]
    if len(unmapped) > 0:
        print(f"\n  Unmapped conditions ({len(unmapped)}):")
        for _, r in unmapped.head(30).iterrows():
            print(f"    \"{r['raw_condition']}\" → KEPT AS-IS (best score={r['similarity_score']:.3f})")

    return mapping_df


def export_review_stats():
    """Summarize the review dataset for the report."""
    if not os.path.exists(REVIEWED_CSV):
        print("No reviewed data found.")
        return

    df = pd.read_csv(REVIEWED_CSV)
    print(f"\nReview Dataset Summary:")
    print(f"  Total reviewed: {len(df)}")
    print(f"  Valid conditions: {(df['reviewed_label'] == 1).sum()}")
    print(f"  Invalid (noise): {(df['reviewed_label'] == 0).sum()}")
    print(f"  Originally accepted: {(df['current_label'] == 1).sum()}")
    print(f"  Originally rejected: {(df['current_label'] == 0).sum()}")

    changed = df[df["current_label"] != df["reviewed_label"]]
    print(f"  Label changed during review: {len(changed)}")
    if len(changed) > 0:
        print(f"    False positives (was accepted, should be rejected): "
              f"{((changed['current_label'] == 1) & (changed['reviewed_label'] == 0)).sum()}")
        print(f"    False negatives (was rejected, should be accepted): "
              f"{((changed['current_label'] == 0) & (changed['reviewed_label'] == 1)).sum()}")


if __name__ == "__main__":
    print("=" * 60)
    print("BERT Condition Mapping - Export")
    print("=" * 60)
    print()

    export_review_stats()
    print()

    mapping = export_full_mapping()
    print()

    export_classifier_onnx()
    print()

    print("Done.")
