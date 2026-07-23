import pandas as pd
import re
import sys
import os
sys.path.insert(0, os.path.dirname(__file__))
from config import RAW_CSV, REVIEWED_CSV, CANONICAL_CSV

NOISE_EXACT = {
    "House Calls", "Prevention", "Healthy", "Healthy Volunteers",
    "Healthy Volunteer", "Healthy Adult Subjects", "Healthy Subjects",
    "Diet", "Diet Intervention", "Dietary Proteins", "Exercise",
    "Aerobic Exercise", "Safety", "Safety Issues", "Nurse's Role",
    "Pain Management", "Human Physiology", "Professional-Patient Relations",
    "Patient Non-Compliance", "Randomized Controlled Trial",
    "Dual Diagnosis", "Nutrition, Healthy", "Comprehensive Physical Therapy",
    "Contingency Management", "Elderly Patients",
    "Patients Who Receive Colonoscopy",
    "Patients Referred to Multidisciplinary Pain Clinics",
    "Healthy Aging", "Appropriate Startup of Acid Suppressive Therapy",
    "Immunosuppressive Treatment", "Chemotherapy Effect",
    "Dual Antiplatelet Therapy",
    "Long-term Effects Secondary to Cancer Therapy in Adults",
    "Long-term Effects Secondary to Cancer Therapy in Children",
    "Wisconsin Registry for Alzheimer's Prevention",
    "Wisconsin Alzheimer's Disease Research Center",
}

NOISE_PATTERNS = [
    r"^the study focuses on",
    r"eg\.\s*trauma",
]

VALID_DISEASE_WITH_APOSTROPHE = re.compile(
    r"'s (Disease|Syndrome|Arteritis|Lymphoma|Sarcoma|Dementia|Role)$", re.IGNORECASE
)


def preseed():
    raw = pd.read_csv(RAW_CSV)
    canonical_df = pd.read_csv(CANONICAL_CSV)
    canonical = set(c.upper().strip() for c in canonical_df["condition"].tolist())

    records = []
    for _, row in raw.iterrows():
        val = row["value"].strip()
        lab = row["label"]
        val_upper = val.upper().strip()

        if lab == 1:
            if val in NOISE_EXACT or any(re.search(p, val, re.IGNORECASE) for p in NOISE_PATTERNS):
                label = 0
            else:
                label = 1
        else:
            if val in NOISE_EXACT:
                label = 0
            elif VALID_DISEASE_WITH_APOSTROPHE.search(val):
                label = 1
            elif val_upper.startswith("AO ") or val_upper.startswith("ICD"):
                label = 0
            elif re.search(r'ICD[- ]?10', val_upper):
                label = 0
            elif re.search(r'CLASSIFICATION\b', val_upper):
                label = 0
            elif "the study focuses on" in val.lower():
                label = 0
            elif val_upper in canonical:
                label = 1
            elif val_upper.replace("'S ", " ") in canonical or val_upper.replace("'S ", "") in canonical:
                label = 1
            else:
                label = 1

        records.append({
            "value": val,
            "current_label": lab,
            "reviewed_label": label,
            "study_nct_id": row["study_nct_id"],
        })

    seeded = pd.DataFrame(records)
    seeded.to_csv(REVIEWED_CSV, index=False)
    print(f"Labeled {len(seeded)} conditions:")
    print(f"  Valid (1): {(seeded['reviewed_label'] == 1).sum()}")
    print(f"  Invalid (0): {(seeded['reviewed_label'] == 0).sum()}")
    print()

    changed = seeded[seeded["current_label"] != seeded["reviewed_label"]]
    print(f"Label changes from original ({len(changed)}):")
    fp = changed[(changed["current_label"] == 1) & (changed["reviewed_label"] == 0)]
    fn = changed[(changed["current_label"] == 0) & (changed["reviewed_label"] == 1)]
    print(f"  False positives fixed (was accepted, now rejected): {len(fp)}")
    for _, r in fp.iterrows():
        print(f"    \"{r['value']}\"")
    print(f"  False negatives fixed (was rejected, now accepted): {len(fn)}")
    for _, r in fn.iterrows():
        print(f"    \"{r['value']}\"")
    print()
    print("You can review/adjust any row by editing data/training-conditions-reviewed.csv")

    return seeded


if __name__ == "__main__":
    preseed()
