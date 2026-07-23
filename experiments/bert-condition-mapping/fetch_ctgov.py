import requests
import pandas as pd
import time
import os
from config import (
    CT_GOV_API, CT_GOV_PAGE_SIZE,
    CT_GOV_STUDIES_CSV, CT_GOV_CONDITIONS_CSV, CT_GOV_KEYWORDS_CSV,
    DATA_DIR,
)


def fetch_studies(page_size: int = CT_GOV_PAGE_SIZE, max_studies: int = 50) -> list[dict]:
    studies = []
    page_token = None
    url = f"{CT_GOV_API}?pageSize={page_size}&format=json"

    while len(studies) < max_studies:
        paginated_url = f"{url}&pageToken={page_token}" if page_token else url
        print(f"  Fetching {len(studies)}/{max_studies} ...")
        resp = requests.get(paginated_url, timeout=30)
        resp.raise_for_status()
        data = resp.json()

        batch = data.get("studies", [])
        studies.extend(batch)

        page_token = data.get("nextPageToken")
        if not page_token:
            break

        time.sleep(0.35)

    return studies[:max_studies]


def extract_condition_rows(studies: list[dict]) -> list[dict]:
    rows = []
    for s in studies:
        ps = s["protocolSection"]
        nct = ps["identificationModule"]["nctId"]
        cm = ps.get("conditionsModule") or {}
        for cond in cm.get("conditions") or []:
            rows.append({"value": cond, "label": 1, "study_nct_id": nct, "source": "condition"})
    return rows


def extract_keyword_rows(studies: list[dict]) -> list[dict]:
    rows = []
    for s in studies:
        ps = s["protocolSection"]
        nct = ps["identificationModule"]["nctId"]
        cm = ps.get("conditionsModule") or {}
        for kw in cm.get("keywords") or []:
            rows.append({"value": kw, "label": 1, "study_nct_id": nct, "source": "keyword"})
    return rows


def fetch():
    os.makedirs(DATA_DIR, exist_ok=True)

    print("Fetching 50 studies from CT.gov API v2 ...")
    studies = fetch_studies()
    print(f"  Got {len(studies)} studies")

    condition_rows = extract_condition_rows(studies)
    keyword_rows = extract_keyword_rows(studies)

    studies_df = pd.DataFrame([{
        "nct_id": s["protocolSection"]["identificationModule"]["nctId"],
        "brief_title": s["protocolSection"]["identificationModule"].get("briefTitle", ""),
        "overall_status": s["protocolSection"]["statusModule"].get("overallStatus", ""),
    } for s in studies])
    studies_df.to_csv(CT_GOV_STUDIES_CSV, index=False)
    print(f"  Saved {len(studies_df)} studies to {CT_GOV_STUDIES_CSV}")

    cond_df = pd.DataFrame(condition_rows)
    cond_df.to_csv(CT_GOV_CONDITIONS_CSV, index=False)
    print(f"  Saved {len(cond_df)} conditions to {CT_GOV_CONDITIONS_CSV}")

    kw_df = pd.DataFrame(keyword_rows)
    kw_df.to_csv(CT_GOV_KEYWORDS_CSV, index=False)
    print(f"  Saved {len(kw_df)} keywords to {CT_GOV_KEYWORDS_CSV}")

    combined = pd.concat([cond_df, kw_df], ignore_index=True)
    print(f"  Total: {len(combined)} rows ({len(cond_df)} conditions + {len(kw_df)} keywords)")

    return combined


if __name__ == "__main__":
    fetch()
