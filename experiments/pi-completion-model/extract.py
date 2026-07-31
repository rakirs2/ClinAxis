"""Extract raw study + investigator rows from the ctgov-pg database."""

import os

import pandas as pd
import psycopg2

import config

STUDIES_QUERY = """
SELECT nct_id,
       overall_status,
       start_date,
       enrollment
FROM studies
"""

INVESTIGATORS_QUERY = """
SELECT i.nct_id,
       i.name,
       i.role,
       i.affiliation
FROM investigators i
"""


def _query_to_frame(conn, query: str, columns: list[str]) -> pd.DataFrame:
    with conn.cursor() as cur:
        cur.execute(query)
        rows = cur.fetchall()
    return pd.DataFrame(rows, columns=columns)


def fetch_studies() -> pd.DataFrame:
    with psycopg2.connect(
        host=config.DB_HOST,
        port=config.DB_PORT,
        dbname=config.DB_NAME,
        user=config.DB_USER,
        password=config.DB_PASSWORD,
    ) as conn:
        return _query_to_frame(conn, STUDIES_QUERY, ["nct_id", "overall_status", "start_date", "enrollment"])


def fetch_investigators() -> pd.DataFrame:
    with psycopg2.connect(
        host=config.DB_HOST,
        port=config.DB_PORT,
        dbname=config.DB_NAME,
        user=config.DB_USER,
        password=config.DB_PASSWORD,
    ) as conn:
        return _query_to_frame(conn, INVESTIGATORS_QUERY, ["nct_id", "name", "role", "affiliation"])


def extract() -> tuple[pd.DataFrame, pd.DataFrame]:
    studies = fetch_studies()
    investigators = fetch_investigators()
    os.makedirs(config.OUTPUT_DIR, exist_ok=True)
    studies.to_parquet(os.path.join(config.OUTPUT_DIR, "raw_studies.parquet"), index=False)
    investigators.to_parquet(os.path.join(config.OUTPUT_DIR, "raw_investigators.parquet"), index=False)
    print(f"studies: {len(studies)} rows, investigators: {len(investigators)} rows")
    return studies, investigators


if __name__ == "__main__":
    extract()
