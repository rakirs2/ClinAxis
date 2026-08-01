import os

# Model features = the export endpoint's encoded columns, excluding identity
# columns (person_id, full_name), meta (batch_size), the audit column
# (rule_score) and the label. rule_score is deliberately NOT a feature: labels
# derive from the rule's own decisions, so including rule_score would let the
# model copy the rule instead of learning an independent signal.
FEATURE_NAMES = [
    "exact_name_match",
    "middle_name_match", "has_middle_name_match",
    "credential_match", "has_credential_match",
    "state_match", "has_state_match",
    "city_match", "has_city_match",
    "org_match", "has_org_match",
    "other_name_match", "has_other_name_match",
    "specialty_match", "has_specialty_match",
    "license_state_match", "has_license_state_match",
    "department_match", "has_department_match",
    "orcid_match",
    "deactivated",
]

LABEL_COLUMN = "label"

# Rule scorer auto-assignment threshold (NpiCandidateScorer.AssignThreshold).
ASSIGN_THRESHOLD = 0.6

OUTPUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data")
MODEL_ARTIFACT_DIR = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..", "..", "Scrapers", "Resources", "npi-model",
)

RANDOM_SEED = 42
TEST_FRACTION = 0.2
