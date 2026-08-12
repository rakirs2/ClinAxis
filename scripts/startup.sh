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
echo "[1/7] Checking PostgreSQL..."
if ! pg_isready -q 2>/dev/null; then
    echo "ERROR: PostgreSQL is not running. Start it with: brew services start postgresql"
    exit 1
fi
echo "  OK"
echo ""

# ──────────────────────────────────────────────
# 2. Kill any existing services on our ports
# ──────────────────────────────────────────────
echo "[2/7] Stopping any existing services..."
lsof -ti:5003 2>/dev/null | xargs -r kill -9 2>/dev/null || true
lsof -ti:5001 2>/dev/null | xargs -r kill -9 2>/dev/null || true
ps aux | grep '/IngestionApp' | grep -v grep | awk '{print $2}' | xargs -r kill -9 2>/dev/null || true
echo "  Ports 5001, 5003 cleared; stale IngestionApp processes killed"
echo ""

# ──────────────────────────────────────────────
# 3. Build all production projects
# ──────────────────────────────────────────────
echo "[3/7] Building production projects..."
find . -name '.msCoverageSourceRootsMapping_*' -delete 2>/dev/null || true
for proj in DataApi Frontend Scrapers.Validation IngestionApp; do
    if ! dotnet build "$proj/" 2>&1 | grep -q "Build succeeded"; then
        echo "  Build of $proj failed"
        exit 1
    fi
done
echo "  Build succeeded"
echo ""

# ──────────────────────────────────────────────
# 4. Export MiniLM MeSH embeddings (if needed) and copy to output dirs
# ──────────────────────────────────────────────
MESH_RESOURCES_OUT="Scrapers/Resources/mesh"
if [ ! -d "$MESH_RESOURCES_OUT" ] || \
   [ ! -f "$MESH_RESOURCES_OUT/mesh_embeddings.bin" ] || \
   [ ! -f "$MESH_RESOURCES_OUT/matcher_config.json" ] || \
   [ -f "$MESH_RESOURCES_OUT/tokenizer.model" ]; then
    echo "[4/7] Exporting MiniLM ONNX model and MeSH embeddings..."
    experiments/bert-condition-mapping/.venv/bin/python \
        experiments/bert-condition-mapping/export_sbert_onnx.py 2>&1 | tail -10
    echo "  ONNX export and embeddings complete"
else
    echo "[4/7] MeSH embeddings already exist, skipping export"
fi

echo "  Copying MeSH model files to output directories..."
for proj in Scrapers.Validation DataApi IngestionApp; do
    PROJ_OUTPUT_DIR="${proj}/bin/Debug/net10.0"
    MESH_RESOURCES_DIR="${PROJ_OUTPUT_DIR}/Resources/mesh"
    mkdir -p "$MESH_RESOURCES_DIR"
    for f in "$MESH_RESOURCES_OUT"/*; do
        [ -f "$f" ] && cp "$f" "$MESH_RESOURCES_DIR/"
    done
done
echo "  Done"
echo ""

# ──────────────────────────────────────────────
# 5. Reset database and seed
# ──────────────────────────────────────────────
echo "[5/7] Resetting database..."
POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project DataApi/ -- --reset-db 2>&1 | tail -1
echo "  Database reset complete"
echo ""

# ──────────────────────────────────────────────
# 6. Start all services
# ──────────────────────────────────────────────
echo "[6/7] Starting services..."
echo "  DataApi:  http://localhost:5003"
echo "  Frontend: http://localhost:5001"

POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project DataApi/ &> /tmp/data-api.log &
DATA_API_PID=$!

for i in $(seq 1 30); do
    if curl -sf http://localhost:5003/health > /dev/null 2>&1; then break; fi
    sleep 2
done
echo "  DataApi healthy"

POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project Frontend/ &> /tmp/frontend.log &
FRONTEND_PID=$!

for i in $(seq 1 30); do
    if curl -sf http://localhost:5001/health > /dev/null 2>&1; then break; fi
    sleep 2
done
echo "  Frontend healthy"

POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    INGESTION_STUDY_LIMIT="$STUDY_LIMIT" \
    dotnet run --project IngestionApp/ &> /tmp/ingestion.log &
INGESTION_PID=$!
echo "  IngestionApp started"
echo ""

# ──────────────────────────────────────────────
# 7. Ingest studies
# ──────────────────────────────────────────────
echo "[7/7] Ingesting ${STUDY_LIMIT} studies from ClinicalTrials.gov..."
POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project Scrapers.Validation/ -- "$STUDY_LIMIT" 2>&1 || true
echo ""

# Clear any DLQ events that accumulated during ingestion
psql -d clinical_trial_data -c "DELETE FROM pipeline_events WHERE status = 'dead-letter';" 2>&1 | tail -1
DLQ_COUNT=$(psql -d clinical_trial_data -t -c "SELECT COUNT(*) FROM pipeline_events WHERE status = 'dead-letter';" 2>/dev/null | xargs)

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
