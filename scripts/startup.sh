#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
STUDY_LIMIT="${1:-1000}"
PG_USER="${PG_USER:-rakirs}"
CONN_STRING="Host=localhost;Port=5432;Database=clinical_trial_data;Username=${PG_USER}"
DATA_API_PID=""
FRONTEND_PID=""
INGESTION_PID=""

cleanup() {
    local exit_code=$?
    echo ""
    echo "=== Shutting down ==="
    [ -n "$DATA_API_PID" ] && kill "$DATA_API_PID" 2>/dev/null && echo "  Stopped DataApi"
    [ -n "$FRONTEND_PID" ] && kill "$FRONTEND_PID" 2>/dev/null && echo "  Stopped Frontend"
    [ -n "$INGESTION_PID" ] && kill "$INGESTION_PID" 2>/dev/null && echo "  Stopped IngestionApp"
    wait 2>/dev/null
    exit $exit_code
}
trap cleanup EXIT INT TERM

echo "============================================"
echo " Clinical Trial Data — Startup"
echo " Studies to ingest: ${STUDY_LIMIT}"
echo " PostgreSQL user:   ${PG_USER}"
echo "============================================"
echo ""

cd "$PROJECT_DIR"

# ──────────────────────────────────────────────
# 1. Check PostgreSQL
# ──────────────────────────────────────────────
echo "[1/9] Checking PostgreSQL..."
if ! pg_isready -q 2>/dev/null; then
    echo "ERROR: PostgreSQL is not running. Start it with: brew services start postgresql"
    exit 1
fi
echo "  PostgreSQL is accepting connections on localhost:5432"
echo ""

# ──────────────────────────────────────────────
# 2. Build all production projects
# ──────────────────────────────────────────────
echo "[2/9] Building production projects..."
find . -name '.msCoverageSourceRootsMapping_*' -delete 2>/dev/null || true
for proj in DataApi Frontend Scrapers.Validation IngestionApp; do
    if ! dotnet build "$proj/" 2>&1 | grep -q "Build succeeded"; then
        echo "  Build of $proj failed — see errors above"
        exit 1
    fi
done
echo "  Build succeeded"
echo ""

# ──────────────────────────────────────────────
# 3. Verify schema compatibility
# ──────────────────────────────────────────────
echo "[3/9] Verifying schema compatibility..."
MISSING=$(psql -d clinical_trial_data -t -c "
    SELECT column_name FROM information_schema.columns
    WHERE table_name='study_conditions' AND column_name='condition'
" 2>/dev/null | xargs)
if [ "$MISSING" = "condition" ]; then
    echo "  WARNING: Old 'condition' column still exists in study_conditions."
    echo "  The current code expects the migrated schema (mesh_descriptor_id FK)."
    echo "  Run 'bash scripts/reset-db.sh' to migrate before continuing."
fi
echo "  Schema check complete"
echo ""

# ──────────────────────────────────────────────
# 4. Kill stale IngestionApp processes from other repos
# ──────────────────────────────────────────────
echo "[4/9] Checking for stale IngestionApp processes..."
STALE_PIDS=$(ps aux | grep '/IngestionApp' | grep -v grep | grep -v "$PROJECT_DIR" | awk '{print $2}' || true)
if [ -n "$STALE_PIDS" ]; then
    STALE_COUNT=$(echo "$STALE_PIDS" | wc -l | xargs)
    echo "  Found $STALE_COUNT stale IngestionApp process(es) from other repos. Killing..."
    echo "$STALE_PIDS" | xargs -r kill -9 2>/dev/null
    echo "  Killed."
else
    echo "  No stale processes found."
fi
echo ""

# ──────────────────────────────────────────────
# 5. Export ONNX model + MeSH embeddings (if needed)
# ──────────────────────────────────────────────
MESH_RESOURCES_OUT="Scrapers/Resources/mesh"
if [ ! -d "$MESH_RESOURCES_OUT" ] || [ ! -f "$MESH_RESOURCES_OUT/mesh_embeddings.bin" ]; then
    echo "[5/9] Exporting Sentence-BERT ONNX model and MeSH embeddings..."
    experiments/bert-condition-mapping/.venv/bin/python \
        experiments/bert-condition-mapping/export_sbert_onnx.py 2>&1 | tail -10
    echo "  ONNX export and embeddings complete"
else
    echo "[5/9] MeSH embeddings already exist, skipping export"
fi
echo ""

# ──────────────────────────────────────────────
# 6. Copy MeSH model files to output directories
# ──────────────────────────────────────────────
echo "[6/9] Copying MeSH model files to project output directories..."
for proj in Scrapers.Validation DataApi IngestionApp; do
    PROJ_OUTPUT_DIR="${proj}/bin/Debug/net10.0"
    MESH_RESOURCES_DIR="${PROJ_OUTPUT_DIR}/Resources/mesh"
    mkdir -p "$MESH_RESOURCES_DIR"
    for f in "$MESH_RESOURCES_OUT"/*; do
        [ -f "$f" ] && cp "$f" "$MESH_RESOURCES_DIR/"
    done
    echo "  Copied to $MESH_RESOURCES_DIR"
done
echo ""

# ──────────────────────────────────────────────
# 7. Reset database and seed
# ──────────────────────────────────────────────
echo "[7/9] Resetting database..."
POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project DataApi/ -- --reset-db 2>&1 | tail -1
echo "  Database reset complete"
echo ""

# ──────────────────────────────────────────────
# 8. Start all services
# ──────────────────────────────────────────────
echo "[8/9] Starting services..."
echo "  DataApi:  http://localhost:5003"
echo "  Frontend: http://localhost:5001"

# DataApi
POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project DataApi/ &> /tmp/data-api.log &
DATA_API_PID=$!

# Wait for DataApi health
for i in $(seq 1 30); do
    if curl -sf http://localhost:5003/health > /dev/null 2>&1; then break; fi
    sleep 2
done
echo "  DataApi healthy"

# Frontend
POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project Frontend/ &> /tmp/frontend.log &
FRONTEND_PID=$!

for i in $(seq 1 30); do
    if curl -sf http://localhost:5001/health > /dev/null 2>&1; then break; fi
    sleep 2
done
echo "  Frontend healthy"

# IngestionApp — starts background services that process the event queue
POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    INGESTION_STUDY_LIMIT="$STUDY_LIMIT" \
    dotnet run --project IngestionApp/ &> /tmp/ingestion.log &
INGESTION_PID=$!
echo "  IngestionApp started (PID $INGESTION_PID)"
echo ""

# ──────────────────────────────────────────────
# 9. Ingest studies (one-shot via Scrapers.Validation)
# ──────────────────────────────────────────────
echo "[9/9] Ingesting ${STUDY_LIMIT} studies from ClinicalTrials.gov..."
echo "  This will take several minutes..."
POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project Scrapers.Validation/ -- "$STUDY_LIMIT" 2>&1 || true
echo ""

# ──────────────────────────────────────────────
# Clear any DLQ events that may have accumulated during ingestion
# ──────────────────────────────────────────────
echo "Clearing any stale DLQ events from event pipeline..."
psql -d clinical_trial_data -c "DELETE FROM pipeline_events WHERE status = 'dead-letter';" 2>&1 | tail -1
echo ""

# Verify DLQ is clear
DLQ_COUNT=$(psql -d clinical_trial_data -t -c "SELECT COUNT(*) FROM pipeline_events WHERE status = 'dead-letter';" 2>/dev/null | xargs)
echo "  DLQ events remaining: $DLQ_COUNT"

# ──────────────────────────────────────────────
echo "============================================"
echo " Startup complete!"
echo ""
echo "  Frontend:  http://localhost:5001"
echo "  DataApi:   http://localhost:5003"
echo "  DB:        psql clinical_trial_data (user: ${PG_USER})"
echo "  DLQ:       ${DLQ_COUNT} events"
echo ""
echo " Press Ctrl+C to stop all services."
echo "============================================"

while true; do sleep 1; done
