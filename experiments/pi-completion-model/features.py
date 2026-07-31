"""Build the training frame: one row per (study in window x PI) with as-of-start features.

Serving parity: the four features mirror what `GetInvestigatorFinderCandidatesAsync`
returns per candidate (StudyCount, CompletedStudies, EnrollmentTotal, CompletionRate).
At serving time the candidate features cover the PI's full history (there is no target
study yet), so in training the features are computed from the PI's history strictly
before the target study's start date.
"""

import re

import pandas as pd

import config

_PUNCT_RE = re.compile(r"[^\w\s]")
_WHITESPACE_RE = re.compile(r"\s+")
_TITLE_RE = re.compile(r"^(dr|prof|mr|mrs|ms|md|phd|m\.d\.|ph\.d\.)\s+", re.IGNORECASE)


def normalize_name(name: str) -> str:
    """Uppercase, strip punctuation/titles, collapse whitespace."""
    if pd.isna(name):
        return ""
    cleaned = _TITLE_RE.sub("", str(name))
    cleaned = _PUNCT_RE.sub(" ", cleaned)
    cleaned = _WHITESPACE_RE.sub(" ", cleaned).strip()
    return cleaned.upper()


def parse_start_date(value) -> pd.Timestamp | None:
    if pd.isna(value):
        return None
    text = str(value).strip()
    if not text:
        return None
    try:
        return pd.Timestamp(text[:7] + "-01")
    except ValueError:
        return None


def build_training_frame(
    studies: pd.DataFrame,
    investigators: pd.DataFrame,
) -> tuple[pd.DataFrame, dict]:
    studies = studies.copy()
    investigators = investigators.copy()

    studies["start"] = studies["start_date"].map(parse_start_date)
    studies["label"] = (studies["overall_status"] == "COMPLETED").astype(int)
    studies = studies.dropna(subset=["start"])

    investigators["name_key"] = investigators["name"].map(normalize_name)
    investigators = investigators.dropna(subset=["name_key"])

    merged = studies.merge(
        investigators[["nct_id", "name_key", "role"]],
        on="nct_id",
        how="inner",
    )

    history = merged[merged["start"].dt.year >= config.HISTORY_YEAR_MIN]
    history = history[history["start"] < pd.Timestamp(f"{config.WINDOW_START_YEAR}-01-01")]

    targets = merged[
        (merged["role"] == "PRINCIPAL_INVESTIGATOR")
        & (merged["start"].dt.year >= config.WINDOW_START_YEAR)
        & (merged["start"].dt.year <= config.WINDOW_END_YEAR)
        & (merged["overall_status"].isin(["COMPLETED", "TERMINATED"]))
    ].copy()
    targets = targets.drop_duplicates(subset=["nct_id", "name_key"])

    prior = (
        history.groupby("name_key")
        .agg(
            priorStudyCount=("nct_id", "nunique"),
            priorCompletedCount=("label", "sum"),
            priorEnrollmentTotal=("enrollment", "sum"),
        )
        .reset_index()
    )
    prior["priorEnrollmentTotal"] = prior["priorEnrollmentTotal"].fillna(0)
    prior["priorCompletionRate"] = (
        prior["priorCompletedCount"] / prior["priorStudyCount"].replace(0, pd.NA)
    ).fillna(0)

    frame = targets.merge(prior, on="name_key", how="left")
    for col in config.FEATURE_NAMES:
        frame[col] = frame[col].fillna(0).astype(float)

    audit = {
        "total_studies_in_window": int(studies[
            (studies["start"].dt.year >= config.WINDOW_START_YEAR)
            & (studies["start"].dt.year <= config.WINDOW_END_YEAR)
        ]["nct_id"].nunique()),
        "window_rows": len(frame),
        "unique_pis": int(frame["name_key"].nunique()),
        "positive_rate": float(frame["label"].mean()),
        "history_studies": int(history["nct_id"].nunique()),
        "history_pis": int(history["name_key"].nunique()),
    }

    return frame[["nct_id", "name_key"] + config.FEATURE_NAMES + ["label"]], audit
