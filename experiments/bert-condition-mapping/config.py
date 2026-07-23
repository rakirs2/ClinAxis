DATA_DIR = "data"
RAW_CSV = f"{DATA_DIR}/training-conditions.csv"
REVIEWED_CSV = f"{DATA_DIR}/training-conditions-reviewed.csv"
CURATED_CSV = f"{DATA_DIR}/curated-conditions.csv"
CANONICAL_CSV = f"{DATA_DIR}/canonical-conditions.csv"
MODEL_DIR = f"{DATA_DIR}/model"
CLASSIFIER_MODEL = f"{MODEL_DIR}/condition-classifier"
ONNX_MODEL = f"{MODEL_DIR}/condition-classifier.onnx"
NORMALIZATION_MODEL = f"{MODEL_DIR}/normalization-model"
EVAL_REPORT = f"{DATA_DIR}/evaluation-report.txt"
MAPPING_TABLE = f"{DATA_DIR}/condition-mapping.csv"

API_BASE_URL = "http://localhost:5003"
TRAINING_URL = f"{API_BASE_URL}/api/export/training/conditions"

BERT_MODEL_NAME = "distilbert-base-uncased"
SENTENCE_BERT_MODEL = "all-MiniLM-L6-v2"
MAX_SEQ_LEN = 64
BATCH_SIZE = 16
EPOCHS = 3
LEARNING_RATE = 2e-5
TEST_SPLIT = 0.2
CLASSIFICATION_THRESHOLD = 0.5
