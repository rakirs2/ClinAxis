#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
STUDY_LIMIT="${1:-1000}"

POSTGRES_CONTAINER="clinicaltrialdata-postgres"
POSTGRES_PORT=5432
POSTGRES_DB="clinical_trial_data"
POSTGRES_USER="${POSTGRES_USER:-postgres}"
CONN_STRING="Host=localhost;Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER}"

DATA_API_PID=""
FRONTEND_PID=""
INGESTION_PID=""

cleanup() {
    local exit_code=$?
    echo ""
    echo "=== Shutting down ==="
    [ -n "$INGESTION_PID" ] && kill "$INGESTION_PID" 2>/dev/null && echo "Stopped ingestion"
    [ -n "$FRONTEND_PID" ] && kill "$FRONTEND_PID" 2>/dev/null && echo "Stopped frontend"
    [ -n "$DATA_API_PID" ] && kill "$DATA_API_PID" 2>/dev/null && echo "Stopped DataApi"
    wait 2>/dev/null
    exit $exit_code
}
trap cleanup EXIT INT TERM

# ---------------------------------------------------------------
# 1. PostgreSQL via Docker
# ---------------------------------------------------------------
ensure_postgres() {
    if docker ps --format '{{.Names}}' | grep -q "^${POSTGRES_CONTAINER}$"; then
        echo "[postgres] Already running in container '${POSTGRES_CONTAINER}'"
        return
    fi

    if docker ps -a --format '{{.Names}}' | grep -q "^${POSTGRES_CONTAINER}$"; then
        echo "[postgres] Starting existing container '${POSTGRES_CONTAINER}'..."
        docker start "$POSTGRES_CONTAINER"
    else
        echo "[postgres] Starting new PostgreSQL 15 container..."
        docker run -d \
            --name "$POSTGRES_CONTAINER" \
            -p "${POSTGRES_PORT}:5432" \
            -e POSTGRES_DB="$POSTGRES_DB" \
            -e POSTGRES_USER="$POSTGRES_USER" \
            -e POSTGRES_HOST_AUTH_METHOD=trust \
            postgres:15
    fi

    echo "[postgres] Waiting for PostgreSQL to accept connections..."
    for i in $(seq 1 30); do
        if docker exec "$POSTGRES_CONTAINER" pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB" &>/dev/null; then
            echo "[postgres] Ready."
            return
        fi
        sleep 1
    done
    echo "[postgres] ERROR: Timed out waiting for PostgreSQL."
    exit 1
}

# ---------------------------------------------------------------
# 2. Reset database
# ---------------------------------------------------------------
reset_db() {
    echo ""
    echo "=== Resetting database ==="
    POSTGRES_CONNECTION_STRING="$CONN_STRING" dotnet run --project "$PROJECT_DIR/DataApi/" -- --reset-db
    echo "Database reset complete."
}

# ---------------------------------------------------------------
# 3. Start services
# ---------------------------------------------------------------
start_service() {
    local name="$1"
    local project="$2"
    local log="$PROJECT_DIR/run-local-$name.log"

    echo "[$name] Starting..."
    POSTGRES_CONNECTION_STRING="$CONN_STRING" \
        dotnet run --project "$project" &> "$log" &
    local pid=$!
    echo "$pid"
    echo "[$name] PID $pid — logging to run-local-$name.log"
}

wait_for_health() {
    local name="$1"
    local url="$2"
    local max_retries="${3:-30}"

    echo "[$name] Waiting for health check at $url ..."
    for i in $(seq 1 "$max_retries"); do
        if curl -sf "$url" > /dev/null 2>&1; then
            echo "[$name] Healthy."
            return
        fi
        sleep 2
    done
    echo "[$name] ERROR: Timed out waiting for health."
    exit 1
}

# ---------------------------------------------------------------
# 4. Ingest studies
# ---------------------------------------------------------------
ingest_studies() {
    local limit="$1"
    echo ""
    echo "=== Ingesting ${limit} studies from ClinicalTrials.gov ==="
    echo "This will take several minutes..."
    POSTGRES_CONNECTION_STRING="$CONN_STRING" \
        dotnet run --project "$PROJECT_DIR/Scrapers.Validation/" -- "$limit" --truncate 2>&1
    echo "Ingestion complete."
}

# ---------------------------------------------------------------
# Main
# ---------------------------------------------------------------
echo "============================================"
echo " Clinical Trial Data — Local Run"
echo " Study limit: ${STUDY_LIMIT}"
echo "============================================"

cd "$PROJECT_DIR"

ensure_postgres
reset_db

echo ""
echo "=== Starting services ==="
DATA_API_PID=$(start_service "data-api" "DataApi/")
FRONTEND_PID=$(start_service "frontend" "Frontend/")

wait_for_health "data-api" "http://localhost:5003/health" 30
wait_for_health "frontend" "http://localhost:5001/health" 30

echo ""
echo "=== All services running ==="
echo "  DataApi:  http://localhost:5003/health"
echo "  Frontend: http://localhost:5001"

ingest_studies "$STUDY_LIMIT"

echo ""
echo "============================================"
echo " Ready!"
echo "  Frontend: http://localhost:5001"
echo "  DataApi:  http://localhost:5003"
echo ""
echo "Press Ctrl+C to stop all services."
echo "============================================"

# Wait indefinitely until Ctrl+C
while true; do sleep 1; done
