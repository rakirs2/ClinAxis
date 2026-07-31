"""Train a logistic regression completion model and export it to ONNX."""

import json
import os
import shutil

import numpy as np
import pandas as pd
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import roc_auc_score
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

    experienced_mask = frame.iloc[x_test_indexes]["priorStudyCount"] > 0
    experienced_auc = (
        roc_auc_score(y_test[experienced_mask], y_proba[experienced_mask])
        if experienced_mask.sum() > 1
        else None
    )

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
        "trainedOn": {
            "windowStart": f"{config.WINDOW_START_YEAR}-01-01",
            "windowEnd": f"{config.WINDOW_END_YEAR}-12-31",
            "testFraction": config.TEST_FRACTION,
        },
        "metrics": {
            "holdoutAUC": round(float(auc), 4),
            "experiencedHoldoutAUC": round(experienced_auc, 4) if experienced_auc is not None else None,
            "coldStartShare": round(float((frame["priorStudyCount"] == 0).mean()), 4),
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
        "experiencedHoldoutAUC": float(experienced_auc) if experienced_auc is not None else None,
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
        "windowRows": len(frame),
        "uniquePis": int(frame["name_key"].nunique()),
        "positiveRate": float(frame["label"].mean()),
    }
    results = train_and_export(frame, audit)
    print(json.dumps(results, indent=2))
    report_path = os.path.join(config.OUTPUT_DIR, "audit_report.json")
    with open(report_path, "w") as f:
        json.dump(results, f, indent=2)


if __name__ == "__main__":
    main()
