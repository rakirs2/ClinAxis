#!/bin/bash
# Resets DB, seeds MeSH descriptors, then starts DataApi + IngestionApp.
# Studies will be ingested from ClinicalTrials.gov (ingestion runs in background).
# DataApi serves on port 5003.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
CONNECTION_STRING="${1:-Host=localhost;Port=5432;Database=clinical_trial_data;Username=$USER}"

cleanup() {
    echo ""
    echo "Shutting down..."
    [ -n "${DATAAPI_PID:-}" ] && kill "$DATAAPI_PID" 2>/dev/null
    [ -n "${INGESTION_PID:-}" ] && kill "$INGESTION_PID" 2>/dev/null
    exit 0
}
trap cleanup SIGINT SIGTERM

echo "=== Preseeded Setup ==="
echo "1) Resetting database (schema + MeSH seed)..."
POSTGRES_CONNECTION_STRING="$CONNECTION_STRING" dotnet run --project "$PROJECT_DIR/DataApi/" -- --reset-db

echo "2) Starting DataApi on http://localhost:5003..."
POSTGRES_CONNECTION_STRING="$CONNECTION_STRING" nohup dotnet run --project "$PROJECT_DIR/DataApi/" > /tmp/dataapi.log 2>&1 &
DATAAPI_PID=$!

echo "3) Starting IngestionApp (fetches real studies from ClinicalTrials.gov)..."
POSTGRES_CONNECTION_STRING="$CONNECTION_STRING" nohup dotnet run --project "$PROJECT_DIR/IngestionApp/" > /tmp/ingestion.log 2>&1 &
INGESTION_PID=$!

for i in $(seq 1 30); do
    if curl -so /dev/null http://localhost:5003/api/distinct-conditions 2>/dev/null; then
        echo "   DataApi ready on http://localhost:5003"
        break
    fi
    sleep 1
done

echo ""
echo "Quick checks:"
echo "  curl http://localhost:5003/api/distinct-conditions | python3 -m json.tool | head -20"
echo "  curl http://localhost:5003/api/mesh-tree | python3 -m json.tool | head -30"
echo "  curl 'http://localhost:5003/api/mesh-tree?branch=C' | python3 -m json.tool | head -30"
echo ""
echo "Monitor ingestion: tail -f /tmp/ingestion.log"
echo "Press Ctrl+C to stop all services."
