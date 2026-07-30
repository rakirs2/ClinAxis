#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
STUDY_LIMIT="${1:-1000}"
PG_USER="${PG_USER:-rakirs}"
CONN_STRING="Host=localhost;Port=5432;Database=clinical_trial_data;Username=${PG_USER}"
DATA_API_PID=""
FRONTEND_PID=""

cleanup() {
    local exit_code=$?
    echo ""
    echo "=== Shutting down ==="
    [ -n "$DATA_API_PID" ] && kill "$DATA_API_PID" 2>/dev/null && echo "Stopped DataApi"
    [ -n "$FRONTEND_PID" ] && kill "$FRONTEND_PID" 2>/dev/null && echo "Stopped Frontend"
    wait 2>/dev/null
    exit $exit_code
}
trap cleanup EXIT INT TERM

echo "============================================"
echo " Clinical Trial Data — Dev Setup"
echo " Studies to ingest: ${STUDY_LIMIT}"
echo " PostgreSQL user:   ${PG_USER}"
echo "============================================"
echo ""

cd "$PROJECT_DIR"

# 1. Check PostgreSQL is running
echo "[1/7] Checking PostgreSQL..."
if ! pg_isready -q 2>/dev/null; then
    echo "ERROR: PostgreSQL is not running. Start it with: brew services start postgresql"
    exit 1
fi
echo "  PostgreSQL is accepting connections on localhost:5432"
echo ""

# 2. Build production projects (skip test projects to avoid stale coverage mapping files)
echo "[2/7] Building production projects..."
find . -name '.msCoverageSourceRootsMapping_*' -delete 2>/dev/null || true
for proj in DataApi Frontend Scrapers.Validation; do
    if ! dotnet build "$proj/" 2>&1 | grep -q "Build succeeded"; then
        dotnet build "$proj/"
        echo "  Build of $proj failed — see errors above"
        exit 1
    fi
done
echo "  Build succeeded"
echo ""

# 3. Export ONNX model + pre-compute MeSH embeddings
MESH_RESOURCES_OUT="Scrapers/Resources/mesh"
if [ ! -d "$MESH_RESOURCES_OUT" ] || [ ! -f "$MESH_RESOURCES_OUT/mesh_embeddings.bin" ]; then
    echo "[3/7] Exporting Sentence-BERT ONNX model and MeSH embeddings..."
    experiments/bert-condition-mapping/.venv/bin/python \
        experiments/bert-condition-mapping/export_sbert_onnx.py 2>&1 | tail -10
    echo "  ONNX export and embeddings complete"
else
    echo "[3/7] MeSH embeddings already exist, skipping export"
fi
echo ""

# 4. Copy MeSH model files to all project output directories
echo "[4/7] Copying MeSH model files to project output directories..."
for proj in Scrapers.Validation DataApi; do
    PROJ_OUTPUT_DIR="${proj}/bin/Debug/net10.0"
    MESH_RESOURCES_DIR="${PROJ_OUTPUT_DIR}/Resources/mesh"
    mkdir -p "$MESH_RESOURCES_DIR"
    for f in "$MESH_RESOURCES_OUT"/*; do
        [ -f "$f" ] && cp "$f" "$MESH_RESOURCES_DIR/"
    done
    echo "  Copied to $MESH_RESOURCES_DIR"
done
echo ""

# 5. Reset database
echo "[5/7] Resetting database..."
POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project DataApi/ -- --reset-db 2>&1 | tail -1
echo "  Database reset complete"
echo ""

# 6. Start DataApi and Frontend
echo "[6/7] Starting services..."
echo "  DataApi:  http://localhost:5003"
echo "  Frontend: http://localhost:5001"
POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project DataApi/ &> /tmp/data-api.log &
DATA_API_PID=$!
POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project Frontend/ &> /tmp/frontend.log &
FRONTEND_PID=$!

echo "  Waiting for health checks..."
for i in $(seq 1 30); do
    if curl -sf http://localhost:5003/health > /dev/null 2>&1; then break; fi
    sleep 2
done
echo "  DataApi healthy."

for i in $(seq 1 30); do
    if curl -sf http://localhost:5001/health > /dev/null 2>&1; then break; fi
    sleep 2
done
echo "  Frontend healthy."
echo ""

# 7. Ingest studies
echo "[7/7] Ingesting ${STUDY_LIMIT} studies from ClinicalTrials.gov..."
echo "  This will take several minutes..."
POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project Scrapers.Validation/ -- "$STUDY_LIMIT" 2>&1 || true
echo ""

# ------------------------------------------------
echo "============================================"
echo " Setup complete!"
echo ""
echo "  Frontend: http://localhost:5001"
echo "  DataApi:  http://localhost:5003"
echo "  DB:       psql clinical_trial_data (user: ${PG_USER})"
echo ""
echo " Press Ctrl+C to stop all services."
echo "============================================"

while true; do sleep 1; done
