import pandas as pd
import numpy as np
import os
import sys
from sklearn.metrics import accuracy_score, precision_recall_fscore_support

from config import RAW_CSV, REVIEWED_CSV, EVAL_REPORT
from train import predict_conditions, load_training_data, CLASSIFIER_MODEL
from normalize import normalize_conditions, load_canonical
from config import CANONICAL_CSV


def evaluate_vs_rule_based():
    print("=" * 60)
    print("Evaluation: BERT Classifier vs Current Rule-Based IsValidCondition")
    print("=" * 60)

    df = load_training_data()

    texts = df["value"].tolist()
    true_labels = df["reviewed_label"].tolist()

    print(f"\nRunning BERT classifier on {len(texts)} conditions ...")
    predictions = predict_conditions(texts, CLASSIFIER_MODEL)

    bert_preds = [1 if p["is_valid"] else 0 for p in predictions]
    bert_probs = [p["probability"] for p in predictions]

    rule_labels = df["current_label"].tolist()

    print("\n--- BERT Classifier vs Ground Truth ---")
    bert_prec, bert_rec, bert_f1, _ = precision_recall_fscore_support(
        true_labels, bert_preds, average="binary"
    )
    bert_acc = accuracy_score(true_labels, bert_preds)
    print(f"  Accuracy : {bert_acc:.4f}")
    print(f"  Precision: {bert_prec:.4f}")
    print(f"  Recall   : {bert_rec:.4f}")
    print(f"  F1       : {bert_f1:.4f}")

    print("\n--- Current Rule-Based vs Ground Truth ---")
    rule_prec, rule_rec, rule_f1, _ = precision_recall_fscore_support(
        true_labels, rule_labels, average="binary"
    )
    rule_acc = accuracy_score(true_labels, rule_labels)
    print(f"  Accuracy : {rule_acc:.4f}")
    print(f"  Precision: {rule_prec:.4f}")
    print(f"  Recall   : {rule_rec:.4f}")
    print(f"  F1       : {rule_f1:.4f}")

    results_df = pd.DataFrame({
        "value": texts,
        "true_label": true_labels,
        "rule_label": rule_labels,
        "bert_label": bert_preds,
        "bert_prob": bert_probs,
    })

    rule_correct = (results_df["rule_label"] == results_df["true_label"]).sum()
    bert_correct = (results_df["bert_label"] == results_df["true_label"]).sum()
    agree = (results_df["rule_label"] == results_df["bert_label"]).sum()

    print(f"\n--- Cross-Comparison ---")
    print(f"  Rule-based correct : {rule_correct}/{len(results_df)} ({100*rule_correct/len(results_df):.1f}%)")
    print(f"  BERT correct       : {bert_correct}/{len(results_df)} ({100*bert_correct/len(results_df):.1f}%)")
    print(f"  Models agree       : {agree}/{len(results_df)} ({100*agree/len(results_df):.1f}%)")

    rule_right_bert_wrong = results_df[(results_df["rule_label"] == results_df["true_label"]) & (results_df["bert_label"] != results_df["true_label"])]
    bert_right_rule_wrong = results_df[(results_df["bert_label"] == results_df["true_label"]) & (results_df["rule_label"] != results_df["true_label"])]

    print(f"\n--- Cases Where Rule is Right but BERT is Wrong ({len(rule_right_bert_wrong)}) ---")
    for _, r in rule_right_bert_wrong.head(15).iterrows():
        print(f"  \"{r['value']}\" (true={r['true_label']}, rule={r['rule_label']}, bert={r['bert_label']}, prob={r['bert_prob']:.3f})")

    print(f"\n--- Cases Where BERT is Right but Rule is Wrong ({len(bert_right_rule_wrong)}) ---")
    for _, r in bert_right_rule_wrong.head(15).iterrows():
        print(f"  \"{r['value']}\" (true={r['true_label']}, rule={r['rule_label']}, bert={r['bert_label']}, prob={r['bert_prob']:.3f})")

    return results_df


def evaluate_normalization():
    print("\n" + "=" * 60)
    print("Evaluation: Normalization Quality")
    print("=" * 60)

    canonical = load_canonical(CANONICAL_CSV)

    test_pairs = [
        ("Diabetes", "Diabetes Mellitus"),
        ("Diabetes Mellitus", "Diabetes Mellitus"),
        ("Type 2 Diabetes", "Type 2 Diabetes"),
        ("Heart Attack", "Myocardial Infarction"),
        ("Myocardial Infarction", "Myocardial Infarction"),
        ("High Blood Pressure", "Hypertension"),
        ("Cancer", "Breast Cancer"),
        ("Concussion", "Concussion"),
        ("COPD", "COPD"),
        ("Alzheimer's", "Alzheimer Disease"),
        ("Depression", "Depression"),
        ("ADHD", "ADHD"),
        ("Stroke", "Stroke"),
        ("CVA", "Cerebrovascular Accident"),
        ("TB", "Tuberculosis"),
        ("HIV", "HIV Infections"),
        ("Obese", "Obesity"),
        ("High Cholesterol", "Hypercholesterolemia"),
        ("Iron Deficiency", "Iron Deficiency Anemia"),
        ("Breast Carcinoma", "Breast Cancer"),
        ("Skin Cancer", "Melanoma"),
        ("Bronchial Asthma", "Asthma"),
        ("Kidney Stones", "Kidney Stones"),
        ("Renal Calculi", "Kidney Stones"),
        ("Cholelithiasis", "Gallstones"),
        ("treatment", "treatment"),
        ("clinical trial", "clinical trial"),
        ("patient treatment", "patient treatment"),
    ]

    results = normalize_conditions([p[0] for p in test_pairs], canonical)

    correct = 0
    total = len(test_pairs)
    print(f"\n{'Input':<30} {'Expected':<30} {'Got':<30} {'Sim':<6}")
    print("-" * 96)
    for r, (inp, expected) in zip(results, test_pairs):
        got = r["canonical_condition"]
        sim = r["similarity_score"]
        is_correct = got == expected or (
            inp == expected and got == inp
        ) or (
            inp.lower().strip() == expected.lower().strip()
        )
        if is_correct:
            correct += 1
        marker = "✓" if is_correct else "✗"
        print(f"{inp:<30} {expected:<30} {got:<30} {sim:<6.4f} {marker}")

    print(f"\nNormalization accuracy: {correct}/{total} ({100*correct/total:.1f}%)")


if __name__ == "__main__":
    results_df = evaluate_vs_rule_based()
    evaluate_normalization()
