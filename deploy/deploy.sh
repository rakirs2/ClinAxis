#!/usr/bin/env bash
set -euo pipefail

HOST="${DEPLOY_HOST:?DEPLOY_HOST not set}"
USER="${DEPLOY_USER:?DEPLOY_USER not set}"
DB_CONN="${PROD_DB_CONNECTION:?PROD_DB_CONNECTION not set}"
SSH_KEY="${DEPLOY_SSH_KEY:?DEPLOY_SSH_KEY not set}"
SKIP_BUILD=false

for arg in "$@"; do
  [[ "$arg" == "--skip-build" ]] && SKIP_BUILD=true
done

cleanup() {
  rm -f "$SSH_KEY_FILE" "${ENV_FILE:-}" "${REMOTE_SCRIPT:-}"
}
trap cleanup EXIT

echo "=== Clinical Trial Data Deploy ==="

if [ "$SKIP_BUILD" = false ]; then
  echo "--- Build & Publish ---"
  dotnet restore
  dotnet build --no-restore -c Release
  dotnet publish DataApi/ -c Release -r linux-x64 --self-contained -o publish/dataapi
  dotnet publish Frontend/ -c Release -r linux-x64 --self-contained -o publish/frontend
  dotnet publish IngestionApp/ -c Release -r linux-x64 --self-contained -o publish/ingestion
  dotnet tool install --global dotnet-ef 2>/dev/null || true
  dotnet ef migrations bundle --project Scrapers/ --self-contained -r linux-x64 -o publish/efbundle
  chmod +x publish/efbundle
fi

echo "--- SSH Setup ---"
SSH_KEY_FILE=$(mktemp)
echo "$SSH_KEY" > "$SSH_KEY_FILE"
chmod 600 "$SSH_KEY_FILE"

remote() {
  ssh -i "$SSH_KEY_FILE" -o ConnectTimeout=10 -o StrictHostKeyChecking=no "$USER@$HOST" "$@"
}
copy() {
  scp -i "$SSH_KEY_FILE" "$@"
}

R="/opt/clinicaltrialdata"

echo "--- Stop services (prevents Restart=always race) ---"
remote "set -e
for svc in clinicaltrialdata-api clinicaltrialdata-frontend clinicaltrialdata-ingestion; do
  sudo systemctl stop \"\$svc\" 2>/dev/null || true
  sudo systemctl reset-failed \"\$svc\" 2>/dev/null || true
done

echo 'Verifying ports free...'
for port in 5003 5001; do
  for i in \$(seq 1 10); do
    if ! ss -tlnp | grep -q \":\$port \"; then
      break
    fi
    sleep 1
  done
  if ss -tlnp | grep -q \":\$port \"; then
    pid=\$(ss -tlnp | grep \":\$port \" | grep -oP 'pid=\\K[0-9]+')
    echo \"Port \$port held by PID \$pid — sending SIGKILL\"
    kill -KILL \"\$pid\" 2>/dev/null || true
    sleep 2
    if ss -tlnp | grep -q \":\$port \"; then
      echo \"FATAL: Port \$port still held after SIGKILL\"
      exit 1
    fi
  fi
done
echo 'All ports free.'"

echo "--- Save rollback ---"
remote "sudo mkdir -p $R && for d in api frontend ingestion; do sudo mv $R/\$d $R/\$d.previous 2>/dev/null || true; done"

echo "--- Copy files ---"
copy -r publish/dataapi/* "$USER@$HOST:$R/api/"
copy -r publish/frontend/* "$USER@$HOST:$R/frontend/"
copy -r publish/ingestion/* "$USER@$HOST:$R/ingestion/"
copy publish/efbundle "$USER@$HOST:$R/efbundle"
copy deploy/systemd/*.service "$USER@$HOST:$R/"
copy deploy/reset-db.sh "$USER@$HOST:$R/reset-db.sh"

if [[ "${RESET_DB:-false}" == "true" ]]; then
  echo "--- Reset database ---"
  remote "POSTGRES_CONNECTION_STRING=\"$DB_CONN\" $R/api/DataApi --reset-db"
  echo "Database reset complete."
fi

echo "--- Write env file ---"
ENV_FILE=$(mktemp)
cat > "$ENV_FILE" <<EOF
POSTGRES_CONNECTION_STRING=$DB_CONN
INGESTION_STUDY_LIMIT=0
ASPNETCORE_ENVIRONMENT=Production
EOF
scp -i "$SSH_KEY_FILE" "$ENV_FILE" "$USER@$HOST:/tmp/clinicaltrialdata.env"
remote "sudo mv /tmp/clinicaltrialdata.env /etc/clinicaltrialdata.env && sudo chmod 600 /etc/clinicaltrialdata.env"

echo "--- Apply migrations (old code stopped, new binaries ready) ---"
remote "chmod +x $R/efbundle && $R/efbundle --connection \"$DB_CONN\""

echo "--- Install systemd units ---"
remote "set -e
for f in clinicaltrialdata-api.service clinicaltrialdata-frontend.service clinicaltrialdata-ingestion.service; do
  echo \"Checking source \$R/\$f...\"
  test -f $R/\$f || { echo \"  ✗ Source \$R/\$f not found\"; exit 1; }
  sudo cp $R/\$f /etc/systemd/system/
  if grep -q KillMode /etc/systemd/system/\$f; then
    echo \"  ✓ \$f installed with KillMode\"
  else
    echo \"  ✗ KillMode missing in copied \$f\"
    exit 1
  fi
done
sudo systemctl daemon-reload
echo 'Systemd units installed.'"

echo "--- Start DataApi ---"
remote "sudo systemctl start clinicaltrialdata-api"

echo "--- Wait for health ---"
remote "for i in \$(seq 1 30); do
  code=\$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 http://localhost:5003/health 2>/dev/null || echo '000')
  if [ \"\$code\" = \"200\" ]; then echo 'DataApi healthy'; break; fi
  if [ \"\$i\" = \"30\" ]; then echo 'FATAL: DataApi health check failed'; exit 1; fi
  sleep 2
done"

echo "--- Smoke test ---"
remote "for i in \$(seq 1 15); do
  code=\$(curl -s -o /dev/null -w '%{http_code}' --max-time 30 http://localhost:5003/api/stats 2>/dev/null || echo '000')
  if [ \"\$code\" = \"200\" ]; then echo 'DB queries working'; break; fi
  if [ \"\$i\" = \"15\" ]; then echo 'FATAL: API stats endpoint failed'; exit 1; fi
  sleep 3
done"

echo "--- Start remaining services ---"
remote "sudo systemctl start clinicaltrialdata-frontend clinicaltrialdata-ingestion"

echo "--- Cleanup rollback ---"
remote "for d in api frontend ingestion; do sudo rm -rf $R/\$d.previous 2>/dev/null || true; done"

echo "=== Deploy complete ==="