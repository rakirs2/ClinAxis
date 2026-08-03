#!/usr/bin/env bash
# Unit tests for scripts/health-check.sh (issue #358).
# Offline: generates fixture JSON with relative timestamps, runs the pure check
# functions and full-script paths. No curl, no network, no Docker.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

# shellcheck source=scripts/health-check.sh
source "$SCRIPT_DIR/health-check.sh"

PASS=0
FAIL=0

fail() {
  echo "FAIL: $1"
  FAIL=$((FAIL + 1))
}

pass() {
  PASS=$((PASS + 1))
}

# assert_finding <message-substring> <actual-output>
assert_finding() {
  local expected="$1"
  local actual="$2"
  if [[ -z "$actual" ]]; then
    fail "expected finding containing '$expected' but got no output"
  elif [[ "$actual" != *"$expected"* ]]; then
    fail "expected finding containing '$expected' but got: $actual"
  else
    pass
  fi
}

# assert_clean <actual-output>
assert_clean() {
  if [[ -n "$1" ]]; then
    fail "expected no findings but got: $1"
  else
    pass
  fi
}

iso_hours_ago() {
  python3 - "$1" <<'PYEOF'
import sys
from datetime import datetime, timedelta, timezone

hours = float(sys.argv[1])
print((datetime.now(timezone.utc) - timedelta(hours=hours)).isoformat().replace('+00:00', 'Z'))
PYEOF
}

# --- fixtures ---
EVENT_QUEUE_OK="$WORK_DIR/event-queue-ok.json"
EVENT_QUEUE_DLQ="$WORK_DIR/event-queue-dlq.json"
DATA_SOURCE_OK="$WORK_DIR/data-source-ok.json"
DATA_SOURCE_STALE="$WORK_DIR/data-source-stale.json"
DATA_SOURCE_STUCK="$WORK_DIR/data-source-stuck.json"
DATA_SOURCE_SPINNING="$WORK_DIR/data-source-spinning.json"
SCRAPER_PROGRESS="$WORK_DIR/scraper-progress.json"

printf '{"deadLetterCount": 3, "pendingCount": 5, "failureRate": 0.02}' > "$EVENT_QUEUE_OK"
printf '{"deadLetterCount": 176, "pendingCount": 0, "failureRate": 0.9}' > "$EVENT_QUEUE_DLQ"

fresh=$(iso_hours_ago 1)
old=$(iso_hours_ago 100)
stuck_since=$(iso_hours_ago 10)

cat > "$DATA_SOURCE_OK" <<EOF
[{"sourceName": "ClinicalTrials.gov", "status": "idle", "lastSyncTimestamp": "$fresh", "updatedAt": "$fresh"}]
EOF
cat > "$DATA_SOURCE_STALE" <<EOF
[{"sourceName": "ClinicalTrials.gov", "status": "idle", "lastSyncTimestamp": "$old", "updatedAt": "$fresh"}]
EOF
cat > "$DATA_SOURCE_STUCK" <<EOF
[{"sourceName": "ClinicalTrials.gov", "status": "syncing", "lastSyncTimestamp": "$stuck_since", "updatedAt": "$fresh"}]
EOF
cat > "$DATA_SOURCE_SPINNING" <<EOF
[{"sourceName": "ClinicalTrials.gov", "status": "syncing", "lastSyncTimestamp": "$fresh", "updatedAt": "$fresh"}]
EOF
printf '{"totalAvailable": 600000, "totalInDb": 37477, "percentScraped": 6.2}' > "$SCRAPER_PROGRESS"

# --- pure check functions ---

echo "== check_dead_letter =="
assert_clean "$(check_dead_letter "$EVENT_QUEUE_OK")"
assert_finding "deadLetterCount=176 exceeds threshold=10" "$(check_dead_letter "$EVENT_QUEUE_DLQ")"
assert_finding "exceeds threshold=2" "$(WATCHDOG_DLQ_THRESHOLD=2 check_dead_letter "$EVENT_QUEUE_OK")"

echo "== check_stale_sync =="
assert_clean "$(check_stale_sync "$DATA_SOURCE_OK")"
assert_finding "stale: ClinicalTrials.gov" "$(check_stale_sync "$DATA_SOURCE_STALE")"
assert_finding "is 100.0h old" "$(check_stale_sync "$DATA_SOURCE_STALE")"
assert_clean "$(WATCHDOG_STALE_HOURS=200 check_stale_sync "$DATA_SOURCE_STALE")"

echo "== check_stuck =="
assert_clean "$(check_stuck "$DATA_SOURCE_OK")"
# A stuck loop (fresh updatedAt but stale lastSyncTimestamp) must still alert.
assert_finding "stuck: ClinicalTrials.gov status=syncing" "$(check_stuck "$DATA_SOURCE_STUCK")"
assert_clean "$(WATCHDOG_STALL_HOURS=24 check_stuck "$DATA_SOURCE_STUCK")"
# A sync in progress (fresh lastSyncTimestamp) must not alert.
assert_clean "$(check_stuck "$DATA_SOURCE_SPINNING")"

echo "== full script: healthy =="
output=$("$SCRIPT_DIR/health-check.sh" --event-queue "$EVENT_QUEUE_OK" --data-source "$DATA_SOURCE_OK")
if [[ $? -ne 0 ]]; then
  fail "healthy run should exit 0"
else
  pass
fi
assert_finding "OK: all watchdog checks passed" "$output"

echo "== full script: DLQ alert =="
set +e
output=$("$SCRIPT_DIR/health-check.sh" --event-queue "$EVENT_QUEUE_DLQ" --data-source "$DATA_SOURCE_OK" \
  --scraper-progress "$SCRAPER_PROGRESS" 2>/dev/null)
rc=$?
set -e
if [[ $rc -ne 1 ]]; then
  fail "alert run should exit 1 (got $rc)"
else
  pass
fi
assert_finding "## Watchdog alert - ClinicalTrialData instance" "$output"
assert_finding "deadLetterCount=176 exceeds threshold=10" "$output"
assert_finding "/api/event-queue/stats" "$output"
assert_finding "/api/scraper-progress" "$output"

echo "== full script: stale + stuck alert =="
set +e
output=$("$SCRIPT_DIR/health-check.sh" --event-queue "$EVENT_QUEUE_OK" --data-source "$DATA_SOURCE_STUCK" 2>/dev/null)
rc=$?
set -e
if [[ $rc -ne 1 ]]; then
  fail "stuck run should exit 1 (got $rc)"
else
  pass
fi
assert_finding "stuck: ClinicalTrials.gov status=syncing" "$output"

echo "== full script: missing payload =="
set +e
output=$("$SCRIPT_DIR/health-check.sh" --event-queue "$WORK_DIR/does-not-exist.json" --data-source "$DATA_SOURCE_OK" 2>/dev/null)
rc=$?
set -e
if [[ $rc -ne 1 ]]; then
  fail "missing payload run should exit 1 (got $rc)"
else
  pass
fi
assert_finding "endpoint payload missing" "$output"

echo "== full script: --alert-file =="
set +e
"$SCRIPT_DIR/health-check.sh" --event-queue "$EVENT_QUEUE_DLQ" --data-source "$DATA_SOURCE_OK" \
  --alert-file "$WORK_DIR/alert.md" 2>/dev/null
rc=$?
set -e
if [[ $rc -ne 1 ]]; then
  fail "--alert-file run should exit 1 (got $rc)"
else
  pass
fi
assert_finding "## Watchdog alert - ClinicalTrialData instance" "$(cat "$WORK_DIR/alert.md")"

echo ""
echo "=== results: $PASS passed, $FAIL failed ==="
[[ $FAIL -eq 0 ]]
