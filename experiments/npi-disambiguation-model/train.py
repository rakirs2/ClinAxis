"""Train a logistic regression NPI disambiguation model and export it to ONNX.

The model scores each NPPES candidate in a batch; the batch's highest-scoring
candidate is the predicted winner. Decision gate (see README): holdout AUC must
be clearly above 0.5 and batch-winner accuracy must beat random selection
(1/batch_size). rule_score is excluded from features, so the model is
independent of the rule it is compared against.

Run: .venv/bin/python train.py
"""

import json
import os

import numpy as np
import pandas as pd
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import precision_score, roc_auc_score
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType

import config

INPUT_NAME = "features"
OUTPUT_NAME = "probabilities"
POSITIVE_CLASS_INDEX = 1


def load_frame() -> pd.DataFrame:
    path = os.path.join(config.OUTPUT_DIR, "training_frame.parquet")
    return pd.read_parquet(path)


def batch_winner_accuracy(frame: pd.DataFrame, proba: np.ndarray) -> float:
    """Share of batches where the model's argmax candidate is the approved one."""
    frame = frame.copy()
    frame["proba"] = proba
    winners = frame.sort_values("proba", ascending=False).groupby("person_id").head(1)
    if len(winners) == 0:
        return float("nan")
    return float(winners[config.LABEL_COLUMN].mean())


def train_and_export(frame: pd.DataFrame, audit: dict) -> dict:
    x = frame[config.FEATURE_NAMES].to_numpy(dtype=np.float32)
    y = frame[config.LABEL_COLUMN].to_numpy()

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

    y_proba = pipeline.predict_proba(x_test)[:, POSITIVE_CLASS_INDEX]
    auc = roc_auc_score(y_test, y_proba)

    test_frame = frame.iloc[x_test_indexes].copy()
    test_frame["proba"] = y_proba
    winner_acc = batch_winner_accuracy(test_frame, y_proba)

    predicted = (y_proba >= config.ASSIGN_THRESHOLD).astype(int)
    precision_at_threshold = (
        precision_score(y_test, predicted, zero_division=0)
        if predicted.sum() > 0
        else 0.0
    )
    mean_batch_size = float(test_frame.groupby("person_id").size().mean())

    initial_types = [(INPUT_NAME, FloatTensorType([None, len(config.FEATURE_NAMES)]))]
    onx = convert_sklearn(
        pipeline,
        initial_types=initial_types,
        target_opset=17,
        options={id(pipeline): {"zipmap": False}},
    )

    schema = {
        "features": config.FEATURE_NAMES,
        "inputName": INPUT_NAME,
        "outputName": OUTPUT_NAME,
        "positiveClassIndex": POSITIVE_CLASS_INDEX,
        "model": "logistic-regression",
        "decision": {
            "rule": "argmax over batch, assignment threshold %s"
            % config.ASSIGN_THRESHOLD,
            "assignThreshold": config.ASSIGN_THRESHOLD,
        },
        "metrics": {
            "holdoutAUC": round(float(auc), 4),
            "batchWinnerAccuracy": round(float(winner_acc), 4),
            "precisionAtAssignThreshold": round(float(precision_at_threshold), 4),
            "meanBatchSize": round(float(mean_batch_size), 4),
            "testFraction": config.TEST_FRACTION,
        },
    }
    schema.update(audit)

    os.makedirs(config.MODEL_ARTIFACT_DIR, exist_ok=True)
    model_path = os.path.join(config.MODEL_ARTIFACT_DIR, "model.onnx")
    schema_path = os.path.join(config.MODEL_ARTIFACT_DIR, "feature_schema.json")
    onx_bytes = onx.SerializeToString()
    with open(model_path, "wb") as f:
        f.write(onx_bytes)
    with open(schema_path, "w") as f:
        json.dump(schema, f, indent=2)

    results = {
        "rows": len(frame),
        "holdoutAUC": float(auc),
        "batchWinnerAccuracy": float(winner_acc),
        "precisionAtAssignThreshold": float(precision_at_threshold),
        "meanBatchSize": float(mean_batch_size),
        "modelBytes": len(onx_bytes),
        "modelPath": model_path,
        "schemaPath": schema_path,
        "trainLabelRate": float(y_train.mean()),
        "testLabelRate": float(y_test.mean()),
        "coefficients": {
            name: float(coef)
            for name, coef in zip(config.FEATURE_NAMES, pipeline.named_steps["lr"].coef_[0])
        },
    }
    return results


def main() -> None:
    frame = load_frame()
    audit = {
        "corpusRows": len(frame),
        "corpusBatches": int(frame["person_id"].nunique()),
        "corpusPositiveRate": float(frame[config.LABEL_COLUMN].mean()),
    }
    results = train_and_export(frame, audit)
    print(json.dumps(results, indent=2))
    report_path = os.path.join(config.OUTPUT_DIR, "audit_report.json")
    with open(report_path, "w") as f:
        json.dump(results, f, indent=2)


if __name__ == "__main__":
    main()
