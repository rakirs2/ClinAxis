"""End-to-end smoke test on synthetic data: features -> train -> ONNX -> predict.

Run: .venv/bin/python smoke_test.py
"""

import json
import os
import tempfile

import numpy as np
import onnxruntime as ort
import pandas as pd

import config
import features
import train

WINDOW = config.WINDOW_START_YEAR


def make_synthetic_data() -> tuple[pd.DataFrame, pd.DataFrame]:
    rng = np.random.default_rng(42)
    n_pis = 30
    pi_names = [f"PI_{i:02d}" for i in range(n_pis)]
    pi_quality = rng.uniform(0.3, 0.9, n_pis)

    studies = []
    investigators = []

    n_history = 300
    for _ in range(n_history):
        year = rng.integers(config.HISTORY_YEAR_MIN, WINDOW)
        nct = f"NCTH{len(studies):06d}"
        pi_idx = int(rng.integers(0, n_pis))
        studies.append({
            "nct_id": nct,
            "overall_status": "COMPLETED" if rng.random() < pi_quality[pi_idx] else "TERMINATED",
            "start_date": f"{year}-03",
            "enrollment": int(rng.integers(20, 500)),
        })
        for _ in range(int(rng.integers(1, 3))):
            investigators.append({
                "nct_id": nct,
                "name": pi_names[pi_idx],
                "role": "PRINCIPAL_INVESTIGATOR",
            })

    n_target = 60
    for _ in range(n_target):
        year = int(rng.integers(WINDOW, WINDOW + 1))
        pi_idx = int(rng.integers(0, n_pis))
        p_complete = pi_quality[pi_idx]
        nct = f"NCTT{len(studies):06d}"
        studies.append({
            "nct_id": nct,
            "overall_status": "COMPLETED" if rng.random() < p_complete else "TERMINATED",
            "start_date": f"{year}-{int(rng.integers(1, 13)):02d}",
            "enrollment": int(rng.integers(20, 500)),
        })
        investigators.append({
            "nct_id": nct,
            "name": pi_names[pi_idx],
            "role": "PRINCIPAL_INVESTIGATOR",
        })

    return pd.DataFrame(studies), pd.DataFrame(investigators)


def verify_onnx(model_path: str, frame: pd.DataFrame) -> None:
    session = ort.InferenceSession(model_path)
    inputs = frame[config.FEATURE_NAMES].to_numpy(dtype=np.float32)[:50]
    outputs = session.run(None, {train.INPUT_NAME: inputs})
    output_names = [o.name for o in session.get_outputs()]
    proba = outputs[output_names.index(train.OUTPUT_NAME)]
    assert proba.shape == (len(inputs), 2), f"unexpected shape {proba.shape}"
    assert np.all(proba >= 0) and np.all(proba <= 1), "probabilities out of [0,1]"
    assert np.allclose(proba.sum(axis=1), 1.0, atol=1e-4), "probabilities do not sum to 1"

    rates = frame["priorCompletionRate"].to_numpy()[:50]
    high = proba[rates > 0.5, 1]
    low = proba[rates <= 0.5, 1]
    if len(high) > 0 and len(low) > 0:
        assert high.mean() > low.mean(), "higher prior completion rate should raise P(completed)"


def main() -> None:
    with tempfile.TemporaryDirectory() as tmp:
        original_out, original_artifact = config.OUTPUT_DIR, config.MODEL_ARTIFACT_DIR
        try:
            config.OUTPUT_DIR = tmp
            config.MODEL_ARTIFACT_DIR = os.path.join(tmp, "artifacts")
            os.makedirs(config.MODEL_ARTIFACT_DIR, exist_ok=True)

            studies, investigators = make_synthetic_data()
            frame, audit = features.build_training_frame(studies, investigators)
            assert len(frame) > 0, "empty training frame"
            assert audit["window_rows"] > 0, "no window rows"
            assert set(config.FEATURE_NAMES + ["label"]).issubset(frame.columns)

            os.makedirs(config.OUTPUT_DIR, exist_ok=True)
            frame.to_parquet(os.path.join(config.OUTPUT_DIR, "training_frame.parquet"), index=False)

            results = train.train_and_export(frame, audit)
            assert results["holdoutAUC"] > 0.5, "synthetic data should be learnable"
            assert results["modelBytes"] > 0

            model_path = os.path.join(config.MODEL_ARTIFACT_DIR, "model.onnx")
            schema_path = os.path.join(config.MODEL_ARTIFACT_DIR, "feature_schema.json")
            assert os.path.exists(model_path)
            with open(schema_path) as f:
                schema = json.load(f)
            assert schema["features"] == config.FEATURE_NAMES
            assert schema["inputName"] == train.INPUT_NAME

            verify_onnx(model_path, frame)
            print("SMOKE TEST PASSED")
        finally:
            config.OUTPUT_DIR = original_out
            config.MODEL_ARTIFACT_DIR = original_artifact


if __name__ == "__main__":
    main()
