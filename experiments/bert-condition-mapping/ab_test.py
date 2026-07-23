import pandas as pd
import re
import os
from config import (
    CT_GOV_CONDITIONS_CSV, CT_GOV_KEYWORDS_CSV, MESH_MAPPING_CSV,
    CT_GOV_STUDIES_CSV, DATA_DIR,
)
from mesh_matcher import match_all as mesh_match
from fetch_ctgov import fetch_studies

ICD_PATTERN = re.compile(
    r"\b[A-TV-Z][0-9][0-9AB]\.?[0-9]{0,4}\b|\b[0-9]{3}\.?[0-9]{0,2}\b"
)


def is_valid_condition_py(condition: str) -> bool:
    if '"' in condition or "'" in condition:
        return False
    if "." in condition:
        return False
    if ICD_PATTERN.search(condition):
        return False
    return True


def run_side_a(cond_df: pd.DataFrame, kw_df: pd.DataFrame) -> pd.DataFrame:
    raw_conds = cond_df[cond_df["value"].apply(is_valid_condition_py)].copy()
    raw_kws = kw_df[kw_df["value"].apply(is_valid_condition_py)].copy()
    raw_conds["side"] = "A"
    raw_kws["side"] = "A"
    raw_conds["result_type"] = "condition"
    raw_kws["result_type"] = "keyword"
    return pd.concat([raw_conds, raw_kws], ignore_index=True)


def run_side_b(mesh_mapping: pd.DataFrame) -> pd.DataFrame:
    out = mesh_mapping.copy()
    out["side"] = "B"
    out["result_type"] = out["origin"].map({
        "condition": "condition",
        "keyword_remapped": "condition_remapped",
        "keyword": "keyword",
    })
    return out


def generate_blind_review_csv(side_a: pd.DataFrame, side_b: pd.DataFrame, path: str):
    studies = pd.read_csv(CT_GOV_STUDIES_CSV)
    title_map = studies.set_index("nct_id")["brief_title"].to_dict()

    rows = []

    for _, r in side_a[side_a["result_type"] == "condition"].iterrows():
        rows.append({
            "nct_id": r["study_nct_id"],
            "title": title_map.get(r["study_nct_id"], ""),
            "item": r["value"],
            "side": "A",
            "mesh_term": "",
            "category": "",
            "similarity": "",
            "notes": "IsValidCondition accepted",
        })

    for _, r in side_b[side_b["origin"].isin(["condition", "keyword_remapped"])].iterrows():
        mesh = r["mesh_name"] if pd.notna(r["mesh_name"]) and r["mesh_name"] != "" else "(unmapped)"
        notes = "remapped from keyword" if r["origin"] == "keyword_remapped" else "direct condition"
        rows.append({
            "nct_id": r["study_nct_id"],
            "title": title_map.get(r["study_nct_id"], ""),
            "item": r["value"],
            "side": "B",
            "mesh_term": mesh,
            "category": r["category"] if pd.notna(r["category"]) else "",
            "similarity": round(r["similarity"], 4) if pd.notna(r["similarity"]) else "",
            "notes": notes,
        })

    df = pd.DataFrame(rows)
    df["review_rating"] = ""
    df = df.sort_values(["nct_id", "side", "item"])
    df.to_csv(path, index=False)
    print(f"  Blind review CSV ({len(df)} rows): {path}")
    return df


A_B_REVIEW_CSV = os.path.join(DATA_DIR, "ab-test-review.csv")
A_B_STATS_TXT = os.path.join(DATA_DIR, "ab-test-stats.txt")


def ab_test():
    print("=" * 60)
    print("A/B Test: IsValidCondition (A) vs MeSH (B)")
    print("=" * 60)

    print("\n--- Step 1: Fetch fresh data from CT.gov ---")
    fetch_studies()

    if not os.path.exists(CT_GOV_CONDITIONS_CSV):
        print(f"  ERROR: {CT_GOV_CONDITIONS_CSV} not found")
        return
    if not os.path.exists(CT_GOV_KEYWORDS_CSV):
        print(f"  ERROR: {CT_GOV_KEYWORDS_CSV} not found")
        return

    cond_df = pd.read_csv(CT_GOV_CONDITIONS_CSV)
    kw_df = pd.read_csv(CT_GOV_KEYWORDS_CSV)
    print(f"\n  Raw conditions: {len(cond_df)}, raw keywords: {len(kw_df)}")

    print("\n--- Step 2: Run Side A (IsValidCondition) ---")
    side_a = run_side_a(cond_df, kw_df)
    a_accepted = len(side_a[side_a["result_type"] == "condition"])
    a_kw = len(side_a[side_a["result_type"] == "keyword"])
    a_rejected = len(cond_df) + len(kw_df) - len(side_a)
    print(f"  Conditions accepted: {a_accepted}")
    print(f"  Keywords accepted:   {a_kw}")
    print(f"  Rejected (by rules): {a_rejected}")

    print("\n--- Step 3: Run Side B (MeSH matcher) ---")
    merged, stats = mesh_match()
    side_b = merged

    b_conds = side_b[side_b["origin"] == "condition"]
    b_remapped = side_b[side_b["origin"] == "keyword_remapped"]
    b_kw = side_b[side_b["origin"] == "keyword"]
    b_total_cond_equivalent = len(b_conds) + len(b_remapped)
    print(f"  Conditions (raw):    {len(b_conds)}")
    print(f"  Remapped from KW:    {len(b_remapped)}")
    print(f"  Condition total:     {b_total_cond_equivalent}")
    print(f"  Keywords (clean):    {len(b_kw)}")
    print(f"  Unmatched:           {(~side_b['matched']).sum()}")

    print("\n--- Step 4: Comparison ---")
    lines = []
    lines.append("=" * 60)
    lines.append("A/B TEST RESULTS")
    lines.append("=" * 60)
    lines.append("")

    lines.append(f"Source: {len(pd.read_csv(CT_GOV_STUDIES_CSV))} studies from CT.gov API v2")
    lines.append(f"Raw conditions: {len(cond_df)}, raw keywords: {len(kw_df)}")
    lines.append("")

    lines.append("-" * 60)
    lines.append("SIDE A — IsValidCondition (current production)")
    lines.append("-" * 60)
    lines.append(f"  Accepted as conditions: {a_accepted}")
    lines.append(f"  Accepted as keywords:   {a_kw}")
    lines.append(f"  Rejected:               {a_rejected}")
    lines.append("")

    lines.append("-" * 60)
    lines.append("SIDE B — MeSH matching (≥0.8)")
    lines.append("-" * 60)
    lines.append(f"  Direct condition matches:      {len(b_conds)}")
    lines.append(f"  Keyword→disease remapped:      {len(b_remapped)}")
    lines.append(f"  Clean keywords (non-disease):  {len(b_kw)}")
    lines.append(f"  Unmatched (below 0.8):         {(~side_b['matched']).sum()}")
    lines.append("")

    lines.append("-" * 60)
    lines.append("CROSS-COMPARISON")
    lines.append("-" * 60)

    shared_ncts = set(side_a["study_nct_id"]) & set(side_b["study_nct_id"])
    lines.append(f"  Studies compared: {len(shared_ncts)}")

    a_only = set(side_a["study_nct_id"]) - set(side_b["study_nct_id"])
    b_only = set(side_b["study_nct_id"]) - set(side_a["study_nct_id"])
    if a_only:
        lines.append(f"  Only in Side A: {len(a_only)}")
    if b_only:
        lines.append(f"  Only in Side B: {len(b_only)}")

    a_cond_set = set(side_a[side_a["result_type"] == "condition"]["value"].str.lower().str.strip())
    b_cond_set = set(
        side_b[side_b["origin"].isin(["condition", "keyword_remapped"])]["value"].str.lower().str.strip()
    )
    overlap = a_cond_set & b_cond_set
    a_unique = a_cond_set - b_cond_set
    b_unique = b_cond_set - a_cond_set
    lines.append(f"  Condition overlap: {len(overlap)}")
    lines.append(f"  Only in Side A:    {len(a_unique)}")
    lines.append(f"  Only in Side B:    {len(b_unique)}")
    lines.append("")

    if a_unique:
        lines.append("  Terms unique to Side A (IsValidCondition accepts, MeSH doesn't):")
        for v in sorted(a_unique)[:10]:
            lines.append(f"    \"{v}\"")
    if b_unique:
        lines.append("  Terms unique to Side B (MeSH accepts, IsValidCondition doesn't):")
        for v in sorted(b_unique)[:10]:
            lines.append(f"    \"{v}\"")

    lines.append("")
    lines.append("-" * 60)
    lines.append("KEYWORD OVERLAP — Terms in both conditions + keywords")
    lines.append("-" * 60)
    a_keyword_set = set(side_a[side_a["result_type"] == "keyword"]["value"].str.lower().str.strip())
    kw_overlap = a_cond_set & a_keyword_set
    lines.append(f"  Terms appearing as BOTH condition + keyword in Side A: {len(kw_overlap)}")
    for v in sorted(kw_overlap)[:15]:
        lines.append(f"    \"{v}\"")

    report = "\n".join(lines)
    print("\n" + report)

    with open(A_B_STATS_TXT, "w") as f:
        f.write(report)
    print(f"\nStats saved to {A_B_STATS_TXT}")

    print("\n--- Step 5: Generate blind review CSV ---")
    generate_blind_review_csv(side_a, side_b, A_B_REVIEW_CSV)

    print("\n--- Summary ---")
    print(f"  Review the CSVs side by side:")
    print(f"    Stats:     {A_B_STATS_TXT}")
    print(f"    Review:    {A_B_REVIEW_CSV}")
    print("  Rate each side's output manually, then tally the winner.")


if __name__ == "__main__":
    ab_test()
