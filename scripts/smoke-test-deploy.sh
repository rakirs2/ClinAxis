#!/usr/bin/env bash
set -euo pipefail

BASE="${1:-http://localhost:5003}"
FAILED=0

check() {
    local url="$1"
    local label="$2"
    local timeout="${3:-30}"
    local status
    status=$(curl -s -o /dev/null -w "%{http_code}" --max-time "$timeout" "$url" 2>/dev/null || echo "000")
    if [ "$status" = "200" ]; then
        echo "  [$status] $label"
    else
        echo "  [$status] $label  <-- FAILED"
        FAILED=1
    fi
}

echo "Smoke testing $BASE ..."

# Warm-up: trigger EF Core model compilation and connection pool before timed checks
for endpoint in /api/investigators?page=1\&pageSize=1 /api/stats /api/distinct-conditions /api/distinct-locations; do
    curl -s -o /dev/null --max-time 60 "$BASE$endpoint" 2>/dev/null || true
done

check "$BASE/health"                            "Health endpoint"
check "$BASE/api/investigators?page=1&pageSize=1" "Investigators list"
check "$BASE/api/stats"                         "Stats endpoint"
check "$BASE/api/distinct-conditions"           "Conditions endpoint"
check "$BASE/api/distinct-locations"            "Locations endpoint"
check "$BASE/api/event-queue/stats"             "Event queue stats"

if [ "$FAILED" = "1" ]; then
    echo "FAILED: Some endpoints returned non-200"
    exit 1
fi
echo "PASSED: All endpoints healthy"
