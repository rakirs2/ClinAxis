import pandas as pd
import numpy as np
import os
import sys
import torch
from torch.utils.data import Dataset, DataLoader
from transformers import (
    DistilBertTokenizer,
    DistilBertForSequenceClassification,
    Trainer,
    TrainingArguments,
    EarlyStoppingCallback,
)
from sklearn.model_selection import train_test_split
from sklearn.metrics import accuracy_score, precision_recall_fscore_support, confusion_matrix
import json

from config import (
    REVIEWED_CSV, CURATED_CSV, CLASSIFIER_MODEL, EVAL_REPORT,
    BERT_MODEL_NAME, MAX_SEQ_LEN, BATCH_SIZE, EPOCHS,
    LEARNING_RATE, TEST_SPLIT, CLASSIFICATION_THRESHOLD,
)


class ConditionDataset(Dataset):
    def __init__(self, texts, labels, tokenizer, max_len):
        self.texts = texts
        self.labels = labels
        self.tokenizer = tokenizer
        self.max_len = max_len

    def __len__(self):
        return len(self.texts)

    def __getitem__(self, idx):
        text = str(self.texts[idx])
        label = self.labels[idx]
        enc = self.tokenizer(
            text,
            truncation=True,
            padding="max_length",
            max_length=self.max_len,
            return_tensors="pt",
        )
        return {
            "input_ids": enc["input_ids"].squeeze(0),
            "attention_mask": enc["attention_mask"].squeeze(0),
            "labels": torch.tensor(label, dtype=torch.long),
        }


def load_training_data():
    if os.path.exists(REVIEWED_CSV):
        df = pd.read_csv(REVIEWED_CSV)
        print(f"Loaded {len(df)} reviewed rows from {REVIEWED_CSV}")
    else:
        print(f"ERROR: {REVIEWED_CSV} not found. Run review.py first.")
        sys.exit(1)

    valid_counts = df["reviewed_label"].value_counts()
    print(f"  Valid (1): {valid_counts.get(1, 0)}")
    print(f"  Invalid (0): {valid_counts.get(0, 0)}")
    return df


def compute_metrics(eval_pred):
    logits, labels = eval_pred
    preds = np.argmax(logits, axis=1)
    precision, recall, f1, _ = precision_recall_fscore_support(labels, preds, average="binary")
    acc = accuracy_score(labels, preds)
    return {"accuracy": acc, "precision": precision, "recall": recall, "f1": f1}


def train():
    print("=" * 60)
    print("BERT Condition Classifier Training")
    print("=" * 60)

    tokenizer = DistilBertTokenizer.from_pretrained(BERT_MODEL_NAME)
    model = DistilBertForSequenceClassification.from_pretrained(
        BERT_MODEL_NAME, num_labels=2
    )

    df = load_training_data()
    texts = df["value"].tolist()
    labels = df["reviewed_label"].tolist()

    train_texts, test_texts, train_labels, test_labels = train_test_split(
        texts, labels, test_size=TEST_SPLIT, random_state=42, stratify=labels
    )

    train_dataset = ConditionDataset(train_texts, train_labels, tokenizer, MAX_SEQ_LEN)
    eval_dataset = ConditionDataset(test_texts, test_labels, tokenizer, MAX_SEQ_LEN)

    os.makedirs(CLASSIFIER_MODEL, exist_ok=True)

    training_args = TrainingArguments(
        output_dir=CLASSIFIER_MODEL,
        num_train_epochs=EPOCHS,
        per_device_train_batch_size=BATCH_SIZE,
        per_device_eval_batch_size=BATCH_SIZE * 2,
        learning_rate=LEARNING_RATE,
        eval_strategy="epoch",
        save_strategy="epoch",
        save_total_limit=2,
        load_best_model_at_end=True,
        metric_for_best_model="f1",
        logging_dir=f"{CLASSIFIER_MODEL}/logs",
        logging_steps=50,
        report_to="none",
        fp16=torch.cuda.is_available(),
    )

    trainer = Trainer(
        model=model,
        args=training_args,
        train_dataset=train_dataset,
        eval_dataset=eval_dataset,
        tokenizer=tokenizer,
        compute_metrics=compute_metrics,
        callbacks=[EarlyStoppingCallback(early_stopping_patience=2)],
    )

    print("\nStarting training ...")
    trainer.train()

    print("\nSaving model ...")
    trainer.save_model(CLASSIFIER_MODEL)
    tokenizer.save_pretrained(CLASSIFIER_MODEL)

    print("\nEvaluating on test set ...")
    eval_results = trainer.evaluate(eval_dataset)

    preds = trainer.predict(eval_dataset)
    logits = preds.predictions
    probas = torch.nn.functional.softmax(torch.tensor(logits), dim=-1).numpy()
    predicted_labels = np.argmax(logits, axis=1)
    true_labels = preds.label_ids

    cm = confusion_matrix(true_labels, predicted_labels)
    tn, fp, fn, tp = cm.ravel()

    report = f"""
{'='*60}
CONDITION CLASSIFIER - EVALUATION REPORT
{'='*60}

Test set size: {len(test_texts)}

Metrics:
  Accuracy : {eval_results['eval_accuracy']:.4f}
  Precision: {eval_results['eval_precision']:.4f}
  Recall   : {eval_results['eval_recall']:.4f}
  F1 Score : {eval_results['eval_f1']:.4f}

Confusion Matrix:
              Predicted:0  Predicted:1
  Actual:0    TN={tn:<6}  FP={fp:<6}
  Actual:1    FN={fn:<6}  TP={tp:<6}

Threshold analysis:
  Threshold | Precision | Recall  | F1
  {'-'*50}
"""
    for thresh in [0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9]:
        adj_preds = (probas[:, 1] >= thresh).astype(int)
        p, r, f, _ = precision_recall_fscore_support(true_labels, adj_preds, average="binary")
        report += f"  {thresh:<9.1f}| {p:<9.4f}| {r:<7.4f}| {f:.4f}\n"

    report += f"""

Training config:
  Model: {BERT_MODEL_NAME}
  Epochs: {EPOCHS}
  Batch size: {BATCH_SIZE}
  Learning rate: {LEARNING_RATE}
  Max seq len: {MAX_SEQ_LEN}
  Test split: {TEST_SPLIT}
  Threshold: {CLASSIFICATION_THRESHOLD}

Training data:
  Total reviewed: {len(df)}
  Valid (1): {(df['reviewed_label'] == 1).sum()}
  Invalid (0): {(df['reviewed_label'] == 0).sum()}
"""
    print(report)

    with open(EVAL_REPORT, "w") as f:
        f.write(report)
    print(f"Report saved to {EVAL_REPORT}")

    df_test = pd.DataFrame({
        "value": test_texts,
        "true_label": true_labels,
        "pred_label": predicted_labels,
        "prob_valid": probas[:, 1],
    })

    misclassified = df_test[df_test["true_label"] != df_test["pred_label"]]
    false_pos = misclassified[misclassified["pred_label"] == 1].head(20)
    false_neg = misclassified[misclassified["pred_label"] == 0].head(20)

    print("\n--- Top False Positives (model said valid, but was invalid) ---")
    for _, r in false_pos.iterrows():
        print(f"  \"{r['value']}\" (prob={r['prob_valid']:.3f})")

    print("\n--- Top False Negatives (model said invalid, but was valid) ---")
    for _, r in false_neg.iterrows():
        print(f"  \"{r['value']}\" (prob={r['prob_valid']:.3f})")


def predict_conditions(conditions: list[str], model_dir: str = CLASSIFIER_MODEL):
    tokenizer = DistilBertTokenizer.from_pretrained(model_dir)
    model = DistilBertForSequenceClassification.from_pretrained(model_dir)
    model.eval()

    results = []
    for cond in conditions:
        enc = tokenizer(cond, truncation=True, padding="max_length",
                        max_length=MAX_SEQ_LEN, return_tensors="pt")
        with torch.no_grad():
            outputs = model(**enc)
            probs = torch.nn.functional.softmax(outputs.logits, dim=-1)
            is_valid = bool(torch.argmax(outputs.logits, dim=-1).item() == 1)
            prob = probs[0, 1].item()
        results.append({"condition": cond, "is_valid": is_valid, "probability": round(prob, 4)})
    return results


if __name__ == "__main__":
    train()
