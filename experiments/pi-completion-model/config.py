import os

DB_HOST = os.environ.get("CTGOV_DB_HOST", "localhost")
DB_PORT = int(os.environ.get("CTGOV_DB_PORT", "55432"))
DB_NAME = os.environ.get("CTGOV_DB_NAME", "clinicaltrials")
DB_USER = os.environ.get("CTGOV_DB_USER", "postgres")
DB_PASSWORD = os.environ.get("CTGOV_DB_PASSWORD", "postgres")

WINDOW_START_YEAR = 2018
WINDOW_END_YEAR = 2019

FEATURE_NAMES = [
    "priorStudyCount",
    "priorCompletedCount",
    "priorEnrollmentTotal",
    "priorCompletionRate",
]

LABEL_COLUMN = "label"

HISTORY_YEAR_MIN = 2000

OUTPUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data")
MODEL_ARTIFACT_DIR = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..", "..", "Scrapers", "Resources", "pi-model",
)

RANDOM_SEED = 42
TEST_FRACTION = 0.2
