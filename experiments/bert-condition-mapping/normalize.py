from sentence_transformers import SentenceTransformer, util
import pandas as pd
import numpy as np
import os
import torch
from config import CANONICAL_CSV, MAPPING_TABLE, CLASSIFIER_MODEL, SENTENCE_BERT_MODEL
from train import predict_conditions


def load_canonical(path: str) -> list[str]:
    df = pd.read_csv(path)
    return df["condition"].tolist()


def normalize_conditions(
    raw_conditions: list[str],
    canonical: list[str],
    threshold: float = 0.5,
    top_k: int = 3,
) -> list[dict]:
    print(f"Loading Sentence-BERT model: {SENTENCE_BERT_MODEL} ...")
    model = SentenceTransformer(SENTENCE_BERT_MODEL)

    print(f"Encoding {len(canonical)} canonical conditions ...")
    canon_embeddings = model.encode(canonical, convert_to_tensor=True, show_progress_bar=True)

    results = []
    print(f"Normalizing {len(raw_conditions)} conditions ...")
    for i in range(0, len(raw_conditions), 16):
        batch = raw_conditions[i : i + 16]
        query_emb = model.encode(batch, convert_to_tensor=True)
        scores = util.cos_sim(query_emb, canon_embeddings)

        for j, cond in enumerate(batch):
            row_scores = scores[j]
            top_indices = torch.topk(row_scores, k=min(top_k, len(canonical))).indices.tolist()
            top_values = [row_scores[idx].item() for idx in top_indices]

            best_idx = top_indices[0]
            best_match = canonical[best_idx]
            best_score = top_values[0]

            results.append({
                "raw_condition": cond,
                "canonical_condition": best_match if best_score >= threshold else cond,
                "similarity_score": round(best_score, 4),
                "is_new": best_score < threshold,
                "alternatives": [
                    {"condition": canonical[top_indices[k]], "score": round(top_values[k], 4)}
                    for k in range(len(top_indices))
                ],
            })

    return results


def export_mapping_table(results: list[dict], path: str):
    rows = []
    for r in results:
        rows.append({
            "raw_condition": r["raw_condition"],
            "canonical_condition": r["canonical_condition"],
            "similarity_score": r["similarity_score"],
            "is_new": r["is_new"],
        })
    df = pd.DataFrame(rows)
    df.to_csv(path, index=False)
    print(f"Mapping table ({len(df)} rows) saved to {path}")
    return df


def compare_with_classifier(results: list[dict]):
    conditions = [r["raw_condition"] for r in results]
    print(f"\nRunning BERT classifier on {len(conditions)} conditions ...")
    predictions = predict_conditions(conditions, CLASSIFIER_MODEL)
    pred_map = {p["condition"]: p for p in predictions}

    for r in results:
        pred = pred_map.get(r["raw_condition"], {})
        r["classifier_valid"] = pred.get("is_valid", None)
        r["classifier_prob"] = pred.get("probability", None)

    return results


if __name__ == "__main__":
    import sys

    canonical = load_canonical(CANONICAL_CSV)
    print(f"Loaded {len(canonical)} canonical conditions")

    test_conditions = [
        "Diabetes", "Diabetes Mellitus", "Type 2 Diabetes",
        "Heart Attack", "Myocardial Infarction",
        "High Blood Pressure", "Hypertension",
        "Cancer", "Malignant Neoplasm",
        "Concussion", "Traumatic Brain Injury",
        "Inflammation", "Chronic Inflammation",
        "COPD", "Chronic Obstructive Pulmonary Disease",
        "Alzheimer's", "Alzheimer Disease",
        "Depression", "Major Depressive Disorder",
        "ADHD", "Stroke", "CVA",
        "COVID-19", "Coronavirus",
        "Obesity", "Obese",
        "Tuberculosis", "TB",
        "HIV", "AIDS",
        "Kidney Stones", "Renal Calculi",
        "Gallstones", "Cholelithiasis",
        "High Cholesterol", "Hypercholesterolemia",
        "Anemia", "Iron Deficiency",
        "Breast Cancer", "Breast Carcinoma",
        "Skin Cancer", "Melanoma",
        "Pneumonia", "Lung Infection",
        "Asthma", "Bronchial Asthma",
        "patient treatment", "treatment", "clinical trial",
        "management of", "safety and efficacy",
        "randomized study", "healthy volunteer",
        "E11.9", "I10", "J45.0",
        "C.O.P.D.", "C A N C E R",
    ]

    results = normalize_conditions(test_conditions, canonical)

    results = compare_with_classifier(results)

    print(f"\n{'='*80}")
    print(f"{'Raw Condition':<35} {'Canonical':<30} {'Sim':<6} {'Valid?':<6}")
    print(f"{'-'*80}")
    for r in results:
        valid_mark = "YES" if r.get("classifier_valid") else "NO" if r.get("classifier_valid") is not None else "?"
        print(f"{r['raw_condition']:<35} {r['canonical_condition']:<30} {r['similarity_score']:<6} {valid_mark:<6}")

    export_mapping_table(results, MAPPING_TABLE)
