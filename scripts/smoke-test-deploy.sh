#!/usr/bin/env bash
set -euo pipefail

BASE="${1:-http://localhost:5003}"
FAILED=0

check() {
    local url="$1"
    local label="$2"
    local status
    status=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$url" 2>/dev/null || echo "000")
    if [ "$status" = "200" ]; then
        echo "  [$status] $label"
    else
        echo "  [$status] $label  <-- FAILED"
        FAILED=1
    fi
}

echo "Smoke testing $BASE ..."
check "$BASE/health"                            "Health endpoint"
check "$BASE/api/investigators?page=1&pageSize=1" "Investigators list"
check "$BASE/api/stats"                         "Stats endpoint"
check "$BASE/api/studies/conditions"            "Conditions endpoint"
check "$BASE/api/studies/locations"             "Locations endpoint"
check "$BASE/api/event-queue/stats"             "Event queue stats"

if [ "$FAILED" = "1" ]; then
    echo "FAILED: Some endpoints returned non-200"
    exit 1
fi
echo "PASSED: All endpoints healthy"
