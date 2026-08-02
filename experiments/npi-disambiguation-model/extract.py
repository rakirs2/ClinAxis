"""Download the NPI candidate training corpus from a live DataApi instance.

The corpus lives only in the application database (person_identifier_candidates),
so extraction goes through the export endpoint — no direct database access.

Run: .venv/bin/python extract.py --base-url http://localhost:5003 \
         --from 2000-01-01 --to <today>
"""

import argparse
import os

import pandas as pd

import config


def fetch(base_url: str, date_from: str, date_to: str) -> pd.DataFrame:
    url = (
        f"{base_url.rstrip('/')}/api/export/training/npi-candidates"
        f"?from={date_from}&to={date_to}"
    )
    frame = pd.read_csv(url, dtype={"person_id": str})
    print(f"fetched {len(frame)} candidate rows from {url}")
    return frame


def extract(base_url: str, date_from: str, date_to: str) -> pd.DataFrame:
    frame = fetch(base_url, date_from, date_to)
    os.makedirs(config.OUTPUT_DIR, exist_ok=True)
    path = os.path.join(config.OUTPUT_DIR, "candidates.parquet")
    frame.to_parquet(path, index=False)
    print(f"wrote {path}")
    return frame


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Extract the NPI disambiguation training corpus from a live DataApi instance."
    )
    parser.add_argument("--base-url", required=True, help="DataApi base URL, e.g. http://localhost:5003")
    parser.add_argument("--from", dest="date_from", required=True,
                        help="Window start (yyyy-MM-dd), filters candidate created_at")
    parser.add_argument("--to", dest="date_to", required=True,
                        help="Window end (yyyy-MM-dd)")
    args = parser.parse_args()
    extract(args.base_url, args.date_from, args.date_to)


if __name__ == "__main__":
    main()
