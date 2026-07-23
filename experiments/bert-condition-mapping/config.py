import os

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, "data")
RAW_CSV = os.path.join(DATA_DIR, "training-conditions.csv")
RAW_KEYWORDS_CSV = os.path.join(DATA_DIR, "training-keywords.csv")
REVIEWED_CSV = os.path.join(DATA_DIR, "training-conditions-reviewed.csv")
CURATED_CSV = os.path.join(DATA_DIR, "curated-conditions.csv")
CANONICAL_CSV = os.path.join(DATA_DIR, "canonical-conditions.csv")
MODEL_DIR = os.path.join(DATA_DIR, "model")
CLASSIFIER_MODEL = os.path.join(MODEL_DIR, "condition-classifier")
ONNX_MODEL = os.path.join(MODEL_DIR, "condition-classifier.onnx")
NORMALIZATION_MODEL = os.path.join(MODEL_DIR, "normalization-model")
EVAL_REPORT = os.path.join(DATA_DIR, "evaluation-report.txt")
MAPPING_TABLE = os.path.join(DATA_DIR, "condition-mapping.csv")

MESH_DIR = os.path.join(DATA_DIR, "mesh")
MESH_DESC_FILE = os.path.join(MESH_DIR, "desc2026.xml")
MESH_TREES_FILE = os.path.join(MESH_DIR, "mtrees2026.txt")
MESH_EMBEDDINGS_FILE = os.path.join(MESH_DIR, "embeddings.npy")
MESH_NAMES_FILE = os.path.join(MESH_DIR, "descriptors.json")
MESH_INDEX_FILE = os.path.join(MESH_DIR, "index.pkl")

CT_GOV_STUDIES_CSV = os.path.join(DATA_DIR, "ctgov-50-studies.csv")
CT_GOV_CONDITIONS_CSV = os.path.join(DATA_DIR, "ctgov-conditions.csv")
CT_GOV_KEYWORDS_CSV = os.path.join(DATA_DIR, "ctgov-keywords.csv")
MESH_MAPPING_CSV = os.path.join(DATA_DIR, "mesh-mapping.csv")
MESH_PER_STUDY_CSV = os.path.join(DATA_DIR, "mesh-per-study.csv")
MESH_REPORT_TXT = os.path.join(DATA_DIR, "mesh-report.txt")

API_BASE_URL = "http://localhost:5003"
TRAINING_URL = f"{API_BASE_URL}/api/export/training/conditions"
TRAINING_KEYWORDS_URL = f"{API_BASE_URL}/api/export/training/keywords"

CT_GOV_API = "https://clinicaltrials.gov/api/v2/studies"
CT_GOV_PAGE_SIZE = 50

MESH_THRESHOLD = 0.8

BERT_MODEL_NAME = "distilbert-base-uncased"
SENTENCE_BERT_MODEL = "all-MiniLM-L6-v2"
MAX_SEQ_LEN = 64
BATCH_SIZE = 16
EPOCHS = 3
LEARNING_RATE = 2e-5
TEST_SPLIT = 0.2
CLASSIFICATION_THRESHOLD = 0.5
