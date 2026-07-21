#!/usr/bin/env bash
set -euo pipefail
# Runs ON the droplet. Stops services, resets the database, and starts services.
# Usage: sudo bash /opt/clinicaltrialdata/reset-db.sh [connection_string]

cs="${1:-$(grep POSTGRES_CONNECTION_STRING /etc/clinicaltrialdata.env | cut -d= -f2-)}"

echo "Stopping services..."
sudo systemctl stop clinicaltrialdata-api clinicaltrialdata-ingestion || true
sleep 2

echo "Resetting database..."
POSTGRES_CONNECTION_STRING="$cs" /opt/clinicaltrialdata/api/DataApi --reset-db

echo "Starting services..."
sudo systemctl start clinicaltrialdata-api clinicaltrialdata-ingestion

echo "Waiting for DataApi..."
for i in $(seq 1 30); do
  code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 http://localhost:5003/health 2>/dev/null || echo "000")
  if [ "$code" = "200" ]; then
    echo "DataApi healthy."
    break
  fi
  if [ "$i" = "30" ]; then
    echo "WARNING: DataApi health check did not return 200"
  fi
  sleep 2
done

echo "Database reset complete."
