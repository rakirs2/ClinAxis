"""End-to-end smoke test on synthetic data: extract -> features -> train -> ONNX -> predict.

Run: .venv/bin/python smoke_test.py
"""

import os
import tempfile

import numpy as np
import onnxruntime as ort
import pandas as pd

import config
import features
import train


def make_synthetic_candidates() -> pd.DataFrame:
    """Generates resolved batches where the approved candidate is the one whose
    identity features match the person profile (learnable signal)."""
    rng = np.random.default_rng(42)
    rows = []
    for person in range(600):
        batch_size = int(rng.integers(2, 5))
        profile_matches = rng.integers(1, batch_size + 1)  # 1..batch_size candidates match
        for position in range(batch_size):
            is_winner = position < profile_matches
            # The true winner has higher match-feature mass.
            if is_winner:
                exact = int(rng.random() < 0.9)
                state = int(rng.random() < 0.8)
                org = int(rng.random() < 0.7)
                specialty = int(rng.random() < 0.6)
            else:
                exact = int(rng.random() < 0.1)
                state = int(rng.random() < 0.2)
                org = int(rng.random() < 0.15)
                specialty = int(rng.random() < 0.1)
            rows.append({
                "person_id": f"P{person:04d}",
                "full_name": f"Person {person}",
                "batch_size": batch_size,
                "exact_name_match": exact,
                "middle_name_match": exact,
                "has_middle_name_match": 1,
                "credential_match": exact,
                "has_credential_match": 1,
                "state_match": state,
                "has_state_match": 1,
                "city_match": state,
                "has_city_match": 1,
                "org_match": org,
                "has_org_match": 1,
                "other_name_match": 0,
                "has_other_name_match": 0,
                "specialty_match": specialty,
                "has_specialty_match": 1,
                "license_state_match": state,
                "has_license_state_match": 1,
                "department_match": specialty,
                "has_department_match": 1,
                "orcid_match": 0,
                "deactivated": 0,
                "rule_score": 0.5,
                "label": 1 if is_winner else 0,
            })
    return pd.DataFrame(rows)


def predict(frame: pd.DataFrame) -> np.ndarray:
    model_path = os.path.join(config.MODEL_ARTIFACT_DIR, "model.onnx")
    schema_path = os.path.join(config.MODEL_ARTIFACT_DIR, "feature_schema.json")
    schema = pd.read_json(schema_path, typ="series") if os.path.exists(schema_path) else None
    assert schema is not None, "feature_schema.json missing"

    session = ort.InferenceSession(model_path)
    input_name = schema["inputName"]
    output_name = schema["outputName"]
    x = frame[config.FEATURE_NAMES].to_numpy(dtype=np.float32)
    output = session.run([output_name], {input_name: x})[0]
    return output[:, schema["positiveClassIndex"]]


def main() -> None:
    with tempfile.TemporaryDirectory() as tmp:
        old_output = config.OUTPUT_DIR
        old_artifact = config.MODEL_ARTIFACT_DIR
        config.OUTPUT_DIR = tmp
        config.MODEL_ARTIFACT_DIR = os.path.join(tmp, "model")
        try:
            candidates = make_synthetic_candidates()
            assert candidates["batch_size"].between(2, 4).all()
            assert candidates.groupby("person_id")["batch_size"].nunique().eq(1).all()
            assert len(candidates) == candidates.groupby("person_id")["batch_size"].first().sum()
            frame, audit = features.build_training_frame(candidates)
            features.save_frame(frame, audit)
            assert frame.shape[0] > 1000, "frame too small"

            results = train.train_and_export(frame, {"corpusRows": len(frame)})
            auc = results["holdoutAUC"]
            winner_acc = results["batchWinnerAccuracy"]
            print(f"holdoutAUC={auc:.4f} batchWinnerAccuracy={winner_acc:.4f}")

            # Learnability gate: the synthetic signal must be learnable.
            assert auc > 0.6, f"model failed to learn synthetic signal (AUC {auc:.4f})"
            assert winner_acc > 0.6, f"batch winner accuracy too low ({winner_acc:.4f})"

            # ONNX round-trip: probabilities in [0,1] and identical to training-time math.
            model_path = os.path.join(config.MODEL_ARTIFACT_DIR, "model.onnx")
            assert os.path.exists(model_path), "model.onnx not written"
            proba = predict(frame)
            assert proba.shape == (len(frame),), "output shape mismatch"
            assert ((proba >= 0) & (proba <= 1)).all(), "probabilities outside [0,1]"
            print("smoke test passed")
        finally:
            config.OUTPUT_DIR = old_output
            config.MODEL_ARTIFACT_DIR = old_artifact


if __name__ == "__main__":
    main()
