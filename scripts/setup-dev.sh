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
echo "[1/5] Checking PostgreSQL..."
if ! pg_isready -q 2>/dev/null; then
    echo "ERROR: PostgreSQL is not running. Start it with: brew services start postgresql"
    exit 1
fi
echo "  PostgreSQL is accepting connections on localhost:5432"
echo ""

# 2. Build production projects (skip test projects to avoid stale coverage mapping files)
echo "[2/5] Building production projects..."
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

# 3. Export ONNX model if not already present
if [ ! -f "mesh_service/model/model.onnx" ]; then
    echo "[3/6] Exporting Sentence-BERT ONNX model..."
    experiments/bert-condition-mapping/.venv/bin/python mesh_service/export_onnx.py 2>&1 | tail -5
    echo "  ONNX model exported"
else
    echo "[3/6] ONNX model already exists, skipping export"
fi
echo ""

# 5. Reset database
echo "[4/6] Resetting database..."
POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project DataApi/ -- --reset-db 2>&1 | tail -1
echo "  Database reset complete"
echo ""

# 6. Start DataApi and Frontend
echo "[5/6] Starting services..."
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

# 5. Ingest studies
echo "[6/6] Ingesting ${STUDY_LIMIT} studies from ClinicalTrials.gov..."
echo "  This will take several minutes..."
POSTGRES_CONNECTION_STRING="$CONN_STRING" \
    dotnet run --project Scrapers.Validation/ -- "$STUDY_LIMIT" --truncate 2>&1 || true
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
