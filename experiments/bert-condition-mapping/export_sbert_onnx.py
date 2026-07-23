#!/usr/bin/env python3
"""
Export all-MiniLM-L6-v2 to ONNX + pre-compute MeSH embeddings.

Outputs to Scrapers/Resources/mesh/:
  - model.onnx          Sentence-BERT transformer backbone
  - tokenizer.json      HuggingFace tokenizer
  - vocab.txt           BERT vocab for C# tokenizer
  - mesh_terms.bin      Pickled dict with names, cuis, tree_numbers, categories
  - mesh_embeddings.bin Raw float32 array (num_terms x 384)
"""

import json
import os
import pickle
import struct
import sys

import numpy as np
import torch
from sentence_transformers import SentenceTransformer
from transformers import AutoTokenizer

sys.path.insert(0, os.path.dirname(__file__))
from config import MESH_INDEX_FILE, MESH_DIR

SBERT_MODEL = "sentence-transformers/all-MiniLM-L6-v2"
OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Scrapers", "Resources", "mesh")
MAX_SEQ_LEN = 128
EMBED_DIM = 384


def export_onnx():
    os.makedirs(OUT_DIR, exist_ok=True)

    print("Loading Sentence-BERT model on CPU ...")
    model = SentenceTransformer(SBERT_MODEL, device="cpu")
    tokenizer = AutoTokenizer.from_pretrained(SBERT_MODEL)

    print("Exporting transformer backbone to ONNX ...")
    dummy_input = tokenizer(
        "test condition for onnx export",
        return_tensors="pt",
        padding="max_length",
        max_length=MAX_SEQ_LEN,
        truncation=True,
    )

    transformer = model[0].auto_model
    transformer.eval()
    transformer.to("cpu")

    dummy_input_cpu = {
        "input_ids": dummy_input["input_ids"].to("cpu"),
        "attention_mask": dummy_input["attention_mask"].to("cpu"),
    }

    torch.onnx.export(
        transformer,
        (dummy_input_cpu["input_ids"], dummy_input_cpu["attention_mask"]),
        os.path.join(OUT_DIR, "model.onnx"),
        input_names=["input_ids", "attention_mask"],
        output_names=["last_hidden_state"],
        dynamic_axes={
            "input_ids": {0: "batch_size"},
            "attention_mask": {0: "batch_size"},
            "last_hidden_state": {0: "batch_size"},
        },
        opset_version=14,
    )
    print(f"  Saved model.onnx")

    tokenizer.save_pretrained(OUT_DIR)
    print(f"  Saved tokenizer files to {OUT_DIR}")

    vocab_path = os.path.join(OUT_DIR, "vocab.txt")
    tokenizer.save_vocabulary(OUT_DIR)
    if not os.path.exists(vocab_path):
        for f in os.listdir(OUT_DIR):
            if f.endswith(".txt") or "vocab" in f:
                os.rename(os.path.join(OUT_DIR, f), vocab_path)
                break
    print(f"  Saved vocab.txt")


def compute_mesh_embeddings():
    if not os.path.exists(MESH_INDEX_FILE):
        print(f"ERROR: {MESH_INDEX_FILE} not found. Run download_mesh.py first.")
        sys.exit(1)

    print("Loading MeSH index ...")
    with open(MESH_INDEX_FILE, "rb") as f:
        index = pickle.load(f)

    names = index["names"]
    cuis = index["cuis"]
    tree_numbers = index["tree_numbers"]
    categories = index["categories"]
    print(f"  {len(names)} MeSH terms loaded")

    print("Loading Sentence-BERT model for encoding ...")
    model = SentenceTransformer(SBERT_MODEL)

    # Deduplicate by CUI: keep first occurrence per descriptor
    seen_cuis = {}
    unique_indices = []
    for i, cui in enumerate(cuis):
        if cui not in seen_cuis:
            seen_cuis[cui] = len(unique_indices)
            unique_indices.append(i)
    unique_names = [names[i] for i in unique_indices]
    print(f"  {len(names)} total terms → {len(unique_names)} unique CUIs")
    print(f"  Duplication ratio: {len(names) / len(unique_names):.1f}x")

    print(f"Encoding {len(unique_names)} unique MeSH terms ...")
    unique_embeddings = model.encode(unique_names, show_progress_bar=True, convert_to_numpy=True)
    print(f"  Embeddings shape: {unique_embeddings.shape}")

    # Build index: for each of the 267K names, which embedding row to use
    embedding_index = [seen_cuis[cui] for cui in cuis]

    mesh_data = {
        "names": names,
        "cuis": cuis,
        "tree_numbers": tree_numbers,
        "categories": categories,
    }

    terms_path = os.path.join(OUT_DIR, "mesh_terms.json")
    with open(terms_path, "w") as f:
        json.dump(mesh_data, f)
    print(f"  Saved mesh_terms.json ({len(names)} terms)")

    emb_path = os.path.join(OUT_DIR, "mesh_embeddings.bin")
    with open(emb_path, "wb") as f:
        f.write(struct.pack("<ii", unique_embeddings.shape[0], unique_embeddings.shape[1]))
        f.write(unique_embeddings.astype(np.float32).tobytes())
    size_mb = os.path.getsize(emb_path) / (1024 * 1024)
    print(f"  Saved mesh_embeddings.bin ({unique_embeddings.shape[0]} x {unique_embeddings.shape[1]}, {size_mb:.1f} MB)")

    idx_path = os.path.join(OUT_DIR, "mesh_term_index.bin")
    with open(idx_path, "wb") as f:
        arr = np.array(embedding_index, dtype=np.int32)
        f.write(arr.tobytes())
    idx_size = os.path.getsize(idx_path) / (1024 * 1024)
    print(f"  Saved mesh_term_index.bin ({len(embedding_index)} int32, {idx_size:.1f} MB)")


def main():
    print("=" * 60)
    print("ONNX Export + MeSH Embedding Pre-computation")
    print("=" * 60)
    print()

    export_onnx()
    print()
    compute_mesh_embeddings()

    print()
    print("Done. Files in:")
    for f in sorted(os.listdir(OUT_DIR)):
        fpath = os.path.join(OUT_DIR, f)
        size = os.path.getsize(fpath)
        print(f"  {f} ({size / 1024:.1f} KB)" if size < 1024 * 1024 else f"  {f} ({size / (1024 * 1024):.1f} MB)")


if __name__ == "__main__":
    main()
