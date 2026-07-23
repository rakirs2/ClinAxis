import os
import pickle
import numpy as np
import torch
from sentence_transformers import SentenceTransformer, util
import pandas as pd
from config import (
    MESH_DIR, MESH_INDEX_FILE, MESH_EMBEDDINGS_FILE,
    CT_GOV_CONDITIONS_CSV, CT_GOV_KEYWORDS_CSV,
    MESH_MAPPING_CSV, MESH_THRESHOLD,
    SENTENCE_BERT_MODEL,
)
from download_mesh import download as download_mesh_data


def load_mesh_index():
    if not os.path.exists(MESH_INDEX_FILE):
        print("  MeSH index not found. Downloading ...")
        download_mesh_data()

    print("  Loading MeSH index ...")
    with open(MESH_INDEX_FILE, "rb") as f:
        index = pickle.load(f)

    print(f"    {len(index['names'])} terms loaded")
    return index


def build_or_load_embeddings(model, names: list[str]) -> np.ndarray:
    if os.path.exists(MESH_EMBEDDINGS_FILE):
        print(f"  Loading cached MeSH embeddings from {MESH_EMBEDDINGS_FILE} ...")
        return np.load(MESH_EMBEDDINGS_FILE)

    print(f"  Encoding {len(names)} MeSH terms with Sentence-BERT ...")
    os.makedirs(MESH_DIR, exist_ok=True)
    embeddings = model.encode(names, show_progress_bar=True, convert_to_numpy=True)
    np.save(MESH_EMBEDDINGS_FILE, embeddings)
    print(f"    Saved embeddings shape {embeddings.shape}")
    return embeddings


def match_conditions(
    values: list[str],
    model: SentenceTransformer,
    mesh_names: list[str],
    mesh_cuis: list[str],
    mesh_trees: list[list[str]],
    mesh_categories: list[str],
    mesh_embeddings: np.ndarray,
    threshold: float = MESH_THRESHOLD,
) -> list[dict]:
    results = []

    print(f"  Matching {len(values)} conditions ...")
    for i in range(0, len(values), 32):
        batch = values[i : i + 32]
        batch_emb = model.encode(batch, convert_to_tensor=True)
        device = batch_emb.device
        mesh_emb_tensor = torch.from_numpy(mesh_embeddings).to(device)
        scores = util.cos_sim(batch_emb, mesh_emb_tensor)

        for j, raw_val in enumerate(batch):
            row_scores = scores[j]
            best_idx = torch.argmax(row_scores).item()
            best_score = row_scores[best_idx].item()

            if best_score >= threshold:
                results.append({
                    "value": raw_val,
                    "mesh_cui": mesh_cuis[best_idx],
                    "mesh_name": mesh_names[best_idx],
                    "tree_numbers": ";".join(mesh_trees[best_idx]),
                    "category": mesh_categories[best_idx],
                    "similarity": round(best_score, 4),
                    "matched": True,
                    "alternatives": "",
                })
            else:
                results.append({
                    "value": raw_val,
                    "mesh_cui": "",
                    "mesh_name": "",
                    "tree_numbers": "",
                    "category": "unmapped",
                    "similarity": round(best_score, 4),
                    "matched": False,
                    "alternatives": "",
                })

    return results


def match_all():
    print("\n--- Loading MeSH ---")
    index = load_mesh_index()
    mesh_names = index["names"]
    mesh_cuis = index["cuis"]
    mesh_trees = index["tree_numbers"]
    mesh_categories = index["categories"]

    print("\n--- Loading Sentence-BERT ---")
    model = SentenceTransformer(SENTENCE_BERT_MODEL)

    mesh_embeddings = build_or_load_embeddings(model, mesh_names)

    if not os.path.exists(CT_GOV_CONDITIONS_CSV):
        print(f"  ERROR: {CT_GOV_CONDITIONS_CSV} not found. Run fetch_ctgov.py first.")
        return

    cond_df = pd.read_csv(CT_GOV_CONDITIONS_CSV)
    kw_df = pd.read_csv(CT_GOV_KEYWORDS_CSV)
    combined = pd.concat([cond_df, kw_df], ignore_index=True)

    unique_values = combined["value"].unique().tolist()
    print(f"\n  {len(combined)} total rows, {len(unique_values)} unique values")

    print("\n--- Matching Conditions + Keywords to MeSH ---")
    results = match_conditions(
        unique_values, model,
        mesh_names, mesh_cuis, mesh_trees, mesh_categories,
        mesh_embeddings,
    )

    mapping_df = pd.DataFrame(results)

    merged = combined.merge(
        mapping_df, on="value", how="left"
    )

    is_keyword = merged["source"] == "keyword"
    is_disease = merged["category"] == "disease"
    merged["origin"] = "condition"
    merged.loc[is_keyword & is_disease, "origin"] = "keyword_remapped"
    merged.loc[is_keyword & ~is_disease, "origin"] = "keyword"

    merged.to_csv(MESH_MAPPING_CSV, index=False)
    print(f"\n  Saved {len(merged)} rows to {MESH_MAPPING_CSV}")

    stats = {
        "total_rows": len(merged),
        "conditions": len(cond_df),
        "keywords": len(kw_df),
        "unique_values": len(unique_values),
        "matched": merged["matched"].sum(),
        "unmatched": (~merged["matched"]).sum(),
        "disease": (merged["category"] == "disease").sum(),
        "procedure": (merged["category"] == "procedure").sum(),
        "unmapped": (merged["category"] == "unmapped").sum(),
        "other_cat": (~merged["category"].isin(["disease", "procedure", "unmapped"])).sum(),
        "remapped_keywords": (merged["origin"] == "keyword_remapped").sum(),
    }
    return merged, stats


if __name__ == "__main__":
    df, stats = match_all()
    for k, v in stats.items():
        print(f"  {k}: {v}")
