"""
Manual review CLI tool for condition training data.
Loads the raw CSV from download_data.py and lets you flag each row:
  y = valid medical condition (keep for training)
  n = not a medical condition (noise/reject)
  s = skip (don't judge now, come back later)
  q = quit (save progress)

By default, only shows rows not yet reviewed. Randomizes order
so you see a representative mix of accepted + rejected.
"""

import pandas as pd
import sys
import os
from config import RAW_CSV, REVIEWED_CSV


def load_raw(path: str) -> pd.DataFrame:
    return pd.read_csv(path)


def load_reviewed(path: str) -> set:
    if not os.path.exists(path):
        return set()
    df = pd.read_csv(path)
    return set(df["value"].tolist())


def review():
    raw = load_raw(RAW_CSV)
    reviewed_values = load_reviewed(REVIEWED_CSV)

    unreviewed = raw[~raw["value"].isin(reviewed_values)].copy()
    unreviewed = unreviewed.sample(frac=1, random_state=42)

    print(f"Loaded {len(raw)} total rows")
    print(f"Already reviewed: {len(reviewed_values)}")
    print(f"Remaining: {len(unreviewed)}")
    print()

    if len(unreviewed) == 0:
        print("All rows reviewed!")
        return

    records = []
    idx = 0
    while idx < len(unreviewed):
        row = unreviewed.iloc[idx]
        val = row["value"]
        label = row["label"]
        nct = row["study_nct_id"]

        label_str = "ACCEPTED" if label == 1 else "REJECTED"
        print(f"\n[{idx + 1}/{len(unreviewed)}]  [{label_str}]  {nct}")
        print(f"  Condition: \"{val}\"")
        print(f"  Length: {len(val)} chars, {len(val.split())} words")
        print()
        print("  Is this a valid medical condition?")
        print("    [y] Yes - valid condition")
        print("    [n] No - not a condition (noise)")
        print("    [s] Skip for now")
        print("    [q] Quit and save")
        print()
        choice = input("  > ").strip().lower()

        if choice == "q":
            break
        elif choice == "s":
            idx += 1
            continue
        elif choice in ("y", "n"):
            is_valid = 1 if choice == "y" else 0
            records.append({
                "value": val,
                "current_label": label,
                "reviewed_label": is_valid,
                "study_nct_id": nct,
            })
            idx += 1
        else:
            print("  Invalid choice. Use y/n/s/q")
            continue

    if records:
        new_df = pd.DataFrame(records)
        if os.path.exists(REVIEWED_CSV):
            existing = pd.read_csv(REVIEWED_CSV)
            combined = pd.concat([existing, new_df], ignore_index=True)
        else:
            combined = new_df
        combined.to_csv(REVIEWED_CSV, index=False)
        print(f"\nSaved {len(records)} new reviews to {REVIEWED_CSV}")
        print(f"Total reviewed: {len(combined)}")
    else:
        print("No new reviews to save")

    total = len(pd.read_csv(REVIEWED_CSV)) if os.path.exists(REVIEWED_CSV) else 0
    remaining = len(raw) - total
    print(f"Progress: {total}/{len(raw)} reviewed ({remaining} remaining)")


if __name__ == "__main__":
    review()
