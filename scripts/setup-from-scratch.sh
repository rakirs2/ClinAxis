#!/bin/bash
# Drops and recreates the database from scratch — schema + MeSH only, no studies.
# DataApi serves on port 5003.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
CONNECTION_STRING="${1:-Host=localhost;Port=5432;Database=clinical_trial_data;Username=$USER}"

cleanup() {
    echo ""
    echo "Shutting down DataApi..."
    [ -n "${DATAAPI_PID:-}" ] && kill "$DATAAPI_PID" 2>/dev/null
    exit 0
}
trap cleanup SIGINT SIGTERM

echo "=== From-Scratch Setup (schema + MeSH, no studies) ==="
echo "1) Resetting database..."
POSTGRES_CONNECTION_STRING="$CONNECTION_STRING" dotnet run --project "$PROJECT_DIR/DataApi/" -- --reset-db

echo "2) Starting DataApi on http://localhost:5003..."
POSTGRES_CONNECTION_STRING="$CONNECTION_STRING" nohup dotnet run --project "$PROJECT_DIR/DataApi/" > /tmp/dataapi.log 2>&1 &
DATAAPI_PID=$!

for i in $(seq 1 15); do
    if curl -so /dev/null http://localhost:5003/api/distinct-conditions 2>/dev/null; then
        echo "   DataApi ready"
        break
    fi
    sleep 1
done

echo ""
echo "Empty database — schema + 31K MeSH descriptors seeded."
echo "  curl http://localhost:5003/api/mesh-tree | python3 -m json.tool | head -30"
echo "  curl http://localhost:5003/api/distinct-conditions"
echo ""
echo "No studies yet. Run IngestionApp separately to fetch data."
echo "Press Ctrl+C to stop."
