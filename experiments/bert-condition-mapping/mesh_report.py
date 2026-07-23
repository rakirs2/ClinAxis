import pandas as pd
import os
from config import (
    MESH_MAPPING_CSV, MESH_PER_STUDY_CSV, MESH_REPORT_TXT,
    CT_GOV_STUDIES_CSV,
)


def generate_per_study_csv(df: pd.DataFrame):
    studies = pd.read_csv(CT_GOV_STUDIES_CSV)
    studies_cols = {r["nct_id"]: r["brief_title"] for _, r in studies.iterrows()}

    type_order = {"condition": 0, "remapped_keyword": 1, "keyword": 2}
    out_rows = []
    for _, row in df.iterrows():
        out_rows.append({
            "nct_id": row["study_nct_id"],
            "title": studies_cols.get(row["study_nct_id"], ""),
            "type": row["origin"],
            "original_value": row["value"],
            "mesh_term": row["mesh_name"] if row["matched"] else "",
            "mesh_cui": row["mesh_cui"] if row["matched"] else "",
            "similarity": row["similarity"],
            "category": row["category"],
            "matched": row["matched"],
            "tree_numbers": row["tree_numbers"] if row["matched"] else "",
        })

    out_df = pd.DataFrame(out_rows)
    out_df["_sort"] = out_df["type"].map(type_order)
    out_df = out_df.sort_values(["nct_id", "_sort", "similarity"], ascending=[True, True, False])
    out_df = out_df.drop(columns=["_sort"])
    out_df.to_csv(MESH_PER_STUDY_CSV, index=False)
    print(f"  Saved {len(out_df)} rows to {MESH_PER_STUDY_CSV}")


def generate_report():
    print("\n--- Generating MeSH Mapping Report ---")

    if not os.path.exists(MESH_MAPPING_CSV):
        print(f"  ERROR: {MESH_MAPPING_CSV} not found. Run 'python run.py mesh' first.")
        return
    df = pd.read_csv(MESH_MAPPING_CSV)

    total = len(df)
    conditions = df[df["source"] == "condition"]
    keywords = df[df["source"] == "keyword"]
    remapped = df[df["origin"] == "keyword_remapped"]

    lines = []
    lines.append("=" * 70)
    lines.append("MeSH MAPPING REPORT")
    lines.append("=" * 70)
    lines.append("")

    lines.append(f"Source data: 50 studies from CT.gov API v2")
    lines.append(f"Total rows:  {total}")
    lines.append(f"  Original conditions: {len(conditions)}")
    lines.append(f"  Original keywords:   {len(keywords)}")
    lines.append(f"  Keyword->disease remapped: {len(remapped)}")
    lines.append("")

    lines.append("-" * 70)
    lines.append("MATCH RATES")
    lines.append("-" * 70)
    lines.append(f"  {'':<20} {'Total':<10} {'Matched':<10} {'Unmatched':<10} {'Rate':<10}")
    lines.append(f"  {'-'*60}")
    for label, grp in [("Conditions", conditions), ("Keywords", keywords), ("Combined", df)]:
        t = len(grp)
        m = grp["matched"].sum()
        u = t - m
        r = m / t * 100 if t > 0 else 0
        lines.append(f"  {label:<20} {t:<10} {m:<10} {u:<10} {r:<10.1f}%")

    lines.append("")
    lines.append("-" * 70)
    lines.append("CATEGORY BREAKDOWN")
    lines.append("-" * 70)
    cat_order = ["disease", "procedure", "other", "unmapped"]
    lines.append(f"  {'Category':<15} {'Conditions':<12} {'Keywords':<12} {'Remapped':<12} {'Total':<10}")
    lines.append(f"  {'-'*62}")
    for cat in cat_order:
        cc = len(conditions[conditions["category"] == cat])
        kc = len(keywords[keywords["category"] == cat])
        rc = len(remapped[(remapped["category"] == cat) | ((~remapped["matched"]) & (cat == "unmapped"))])
        tc = cc + kc
        lines.append(f"  {cat:<15} {cc:<12} {kc:<12} {rc:<12} {tc:<10}")

    lines.append("")
    lines.append("-" * 70)
    lines.append("TOP 20 UNMAPPED VALUES (by best similarity score)")
    lines.append("-" * 70)
    unmapped = df[~df["matched"]].sort_values("similarity", ascending=False)
    for _, row in unmapped.head(20).iterrows():
        src = "C" if row["source"] == "condition" else "K"
        lines.append(f"  [{src}] sim={row['similarity']:.4f}  \"{row['value']}\"")

    lines.append("")
    lines.append("-" * 70)
    lines.append("KEYWORD -> DISEASE REMAPPING")
    lines.append("-" * 70)
    lines.append(f"  Keywords remapped to conditions: {len(remapped)}")
    if len(remapped) > 0:
        lines.append("  These keywords matched disease MeSH terms and are now treated as conditions:")
        for _, row in remapped.iterrows():
            status = "✓" if row["matched"] else "✗"
            lines.append(f"    [{status}] \"{row['value']}\" → {row['mesh_name']} (sim={row['similarity']:.4f})")

    lines.append("")
    lines.append("-" * 70)
    lines.append("PER-STUDY SUMMARY (Top 30 by condition count)")
    lines.append("-" * 70)
    per_study = df.groupby("study_nct_id").agg(
        conditions=("origin", lambda x: (x == "condition").sum()),
        remapped=("origin", lambda x: (x == "keyword_remapped").sum()),
        keywords=("origin", lambda x: (x == "keyword").sum()),
        total=("origin", "count"),
    ).reset_index()
    per_study = per_study.sort_values("conditions", ascending=False).head(30)
    lines.append(f"  {'NCT ID':<15} {'Cond':<6} {'Remap':<6} {'KW':<6} {'Total':<6}")
    lines.append(f"  {'-'*40}")
    for _, row in per_study.iterrows():
        r_flag = " ←" if row["remapped"] > 0 else ""
        lines.append(f"  {row['study_nct_id']:<15} {row['conditions']:<6} {row['remapped']:<6} {row['keywords']:<6} {row['total']:<6}{r_flag}")

    lines.append("")
    lines.append("-" * 70)
    lines.append("CONDITIONS BY TREE (Top 20 tree prefixes)")
    lines.append("-" * 70)
    matched_df = df[df["matched"]]
    tree_prefixes = matched_df["tree_numbers"].dropna().str.split(";").explode().str.split(".").str[0]
    top_trees = tree_prefixes.value_counts().head(20)
    for prefix, count in top_trees.items():
        lines.append(f"  {prefix:<6} {count:<5} matches")

    report = "\n".join(lines)
    print("\n" + report)

    with open(MESH_REPORT_TXT, "w") as f:
        f.write(report)
    print(f"\nReport saved to {MESH_REPORT_TXT}")

    generate_per_study_csv(df)


if __name__ == "__main__":
    generate_report()
