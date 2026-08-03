#!/usr/bin/env bash
# Watchdog health checks for the ClinicalTrialData instance (#358).
#
# Evaluates DataApi endpoint payloads against alert thresholds:
#   1. DLQ:   event-queue JSON  -> deadLetterCount > WATCHDOG_DLQ_THRESHOLD
#   2. Stale: data-source JSON  -> lastSyncTimestamp older than WATCHDOG_STALE_HOURS
#   3. Stuck: data-source JSON  -> status != "idle" and lastSyncTimestamp older than
#      WATCHDOG_STALL_HOURS. lastSyncTimestamp (not updatedAt) measures pipeline progress:
#      the scrape loop rewrites updatedAt every ~10s even while stalled.
#
# Endpoint JSON is read from files (the workflow fetches them over SSH; tests use fixtures).
# Exit codes: 0 = healthy, 1 = alert (alert body written to --alert-file or stdout).

set -euo pipefail

WATCHDOG_DLQ_THRESHOLD="${WATCHDOG_DLQ_THRESHOLD:-10}"
WATCHDOG_STALE_HOURS="${WATCHDOG_STALE_HOURS:-6}"
WATCHDOG_STALL_HOURS="${WATCHDOG_STALL_HOURS:-6}"

usage() {
  cat <<'EOF'
Usage: health-check.sh [options]

  --event-queue FILE        JSON payload of /api/event-queue/stats (required)
  --data-source FILE        JSON payload of /api/data-source-state (required)
  --scraper-progress FILE   JSON payload of /api/scraper-progress (optional; included in alert body)
  --alert-file FILE         write the alert body to FILE (default: stdout)
  --dlq-threshold N         dead-letter alert threshold (default: 10)
  --stale-hours N           max age of lastSyncTimestamp in hours (default: 6)
  --stall-hours N           max age of lastSyncTimestamp while status != idle (default: 6)

Env overrides: WATCHDOG_DLQ_THRESHOLD, WATCHDOG_STALE_HOURS, WATCHDOG_STALL_HOURS
EOF
}

# Pure check functions: take a JSON file path, print findings (one per line) to stdout,
# print nothing when the check passes. Safe to source from tests.

check_dead_letter() {
  python3 - "$1" "$WATCHDOG_DLQ_THRESHOLD" <<'PYEOF'
import json, sys

path, threshold = sys.argv[1], int(sys.argv[2])
try:
    count = int(json.load(open(path)).get('deadLetterCount') or 0)
except (ValueError, json.JSONDecodeError):
    print("event-queue: payload unparseable")
    sys.exit(0)
if count > threshold:
    print(f"event-queue: deadLetterCount={count} exceeds threshold={threshold}")
PYEOF
}

check_stale_sync() {
  python3 - "$1" "$WATCHDOG_STALE_HOURS" <<'PYEOF'
import json, re, sys
from datetime import datetime, timezone

def parse(ts):
    if not ts:
        return None
    ts = ts.replace('Z', '+00:00')
    ts = re.sub(r'(\.\d{6})\d+', r'\1', ts)
    try:
        parsed = datetime.fromisoformat(ts)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed

path, threshold_h = sys.argv[1], float(sys.argv[2])
try:
    states = json.load(open(path))
except (ValueError, json.JSONDecodeError):
    print("data-source: payload unparseable")
    sys.exit(0)
now = datetime.now(timezone.utc)
for s in states:
    name = s.get('sourceName') or '?'
    ts = parse(s.get('lastSyncTimestamp'))
    if ts is None:
        print(f"stale: {name} has no lastSyncTimestamp (never synced)")
        continue
    age_h = (now - ts).total_seconds() / 3600.0
    if age_h > threshold_h:
        print(f"stale: {name} lastSyncTimestamp={s.get('lastSyncTimestamp')} is {age_h:.1f}h old (>{threshold_h:g}h)")
PYEOF
}

check_stuck() {
  python3 - "$1" "$WATCHDOG_STALL_HOURS" <<'PYEOF'
import json, re, sys
from datetime import datetime, timezone

def parse(ts):
    if not ts:
        return None
    ts = ts.replace('Z', '+00:00')
    ts = re.sub(r'(\.\d{6})\d+', r'\1', ts)
    try:
        parsed = datetime.fromisoformat(ts)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed

path, stall_h = sys.argv[1], float(sys.argv[2])
try:
    states = json.load(open(path))
except (ValueError, json.JSONDecodeError):
    print("data-source: payload unparseable")
    sys.exit(0)
now = datetime.now(timezone.utc)
for s in states:
    name = s.get('sourceName') or '?'
    status = s.get('status') or ''
    if status == 'idle':
        continue
    ts = parse(s.get('lastSyncTimestamp'))
    if ts is None:
        print(f"stuck: {name} status={status} has no lastSyncTimestamp (never progressed)")
        continue
    age_h = (now - ts).total_seconds() / 3600.0
    if age_h > stall_h:
        print(f"stuck: {name} status={status} lastSyncTimestamp={s.get('lastSyncTimestamp')} "
              f"is {age_h:.1f}h old (>{stall_h:g}h)")
PYEOF
}

check_backfill_stuck() {
  python3 - "$1" "$2" <<'PYEOF'
import json, sys

# Alerts when a sweep is marked in-progress but no chunk events are pending/processing.
# Covers: all chunks dead-lettered (the next scrape tick marks the sweep failed, but the
# watchdog must not wait ~30 min), or chunk events lost after a partial enqueue.
data_path, queue_path = sys.argv[1], sys.argv[2]
try:
    states = json.load(open(data_path))
except (ValueError, json.JSONDecodeError):
    print("backfill: data-source payload unparseable")
    sys.exit(0)

chunk_active = 0
try:
    queue = json.load(open(queue_path))
    for t in queue.get('byEventType', []):
        if t.get('eventType') == 'studies.backfill':
            chunk_active = int(t.get('pending') or 0) + int(t.get('processing') or 0)
except (ValueError, json.JSONDecodeError):
    chunk_active = -1

for s in states:
    if (s.get('backfillStatus') or '') != 'in-progress':
        continue
    remaining = s.get('backfillRemainingStudies')
    if chunk_active == 0 and remaining is not None and remaining > 0:
        print(f"backfill: sweep in-progress but no chunk events active (remaining={remaining})")
PYEOF
}

main() {
  local ALERT_FILE=""
  local EVENT_QUEUE_JSON=""
  local DATA_SOURCE_JSON=""
  local SCRAPER_PROGRESS_JSON=""
  local -a FAILURES=()

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --event-queue) EVENT_QUEUE_JSON="${2:-}"; shift 2 ;;
      --data-source) DATA_SOURCE_JSON="${2:-}"; shift 2 ;;
      --scraper-progress) SCRAPER_PROGRESS_JSON="${2:-}"; shift 2 ;;
      --alert-file) ALERT_FILE="${2:-}"; shift 2 ;;
      --dlq-threshold) WATCHDOG_DLQ_THRESHOLD="${2:-}"; shift 2 ;;
      --stale-hours) WATCHDOG_STALE_HOURS="${2:-}"; shift 2 ;;
      --stall-hours) WATCHDOG_STALL_HOURS="${2:-}"; shift 2 ;;
      -h | --help) usage; exit 0 ;;
      *) echo "Unknown option: $1" >&2; usage; exit 2 ;;
    esac
  done

  if [[ -z "$EVENT_QUEUE_JSON" || -z "$DATA_SOURCE_JSON" ]]; then
    echo "Missing required payload argument" >&2
    usage
    exit 2
  fi

  # Collect findings from a check function; empty output = pass.
  collect() {
    local out
    out=$("$@" 2>&1 || true)
    if [[ -n "$out" ]]; then
      FAILURES+=("$out")
    fi
  }

  if [[ -f "$EVENT_QUEUE_JSON" ]]; then
    collect check_dead_letter "$EVENT_QUEUE_JSON"
  else
    FAILURES+=("event-queue: endpoint payload missing (fetch failed?)")
  fi

  if [[ -f "$DATA_SOURCE_JSON" ]]; then
    collect check_stale_sync "$DATA_SOURCE_JSON"
    collect check_stuck "$DATA_SOURCE_JSON"
    collect check_backfill_stuck "$DATA_SOURCE_JSON" "$EVENT_QUEUE_JSON"
  else
    FAILURES+=("data-source: endpoint payload missing (fetch failed?)")
  fi

  build_body() {
    local checked_at
    checked_at=$(date -u +%Y-%m-%dT%H:%M:%SZ)
    {
      echo "## Watchdog alert - ClinicalTrialData instance"
      echo ""
      echo "- Checked at (UTC): $checked_at"
      echo "- Failures: ${#FAILURES[@]}"
      echo ""
      echo "### Findings"
      printf -- "- %s\n" "${FAILURES[@]}"
      echo ""
      echo "### Endpoint payloads"
      if [[ -f "$EVENT_QUEUE_JSON" ]]; then
        echo ""
        echo "**/api/event-queue/stats**"
        echo '```json'
        cat "$EVENT_QUEUE_JSON"
        echo '```'
      fi
      if [[ -f "$DATA_SOURCE_JSON" ]]; then
        echo ""
        echo "**/api/data-source-state**"
        echo '```json'
        cat "$DATA_SOURCE_JSON"
        echo '```'
      fi
      if [[ -n "$SCRAPER_PROGRESS_JSON" && -f "$SCRAPER_PROGRESS_JSON" ]]; then
        echo ""
        echo "**/api/scraper-progress**"
        echo '```json'
        cat "$SCRAPER_PROGRESS_JSON"
        echo '```'
      fi
    }
  }

  if [[ ${#FAILURES[@]} -eq 0 ]]; then
    echo "OK: all watchdog checks passed"
    exit 0
  fi

  if [[ -n "$ALERT_FILE" ]]; then
    build_body > "$ALERT_FILE"
    echo "ALERT: $(printf '%s; ' "${FAILURES[@]}")" >&2
  else
    build_body
  fi
  exit 1
}

if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
  main "$@"
fi
