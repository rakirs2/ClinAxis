# Deployment Instructions - Supervisord Edition

## Overview

This document provides step-by-step instructions for deploying Clinical Trial Data to your DigitalOcean droplet using Supervisord for process management.

## Quick Start

SSH into your droplet and run:

```bash
curl -fsSL https://raw.githubusercontent.com/rakirs2/ClinicalTrialData/main/deploy/setup.sh | sudo bash
```

Then copy the `PROD_DB_CONNECTION` output and add it to GitHub secrets.

---

## Detailed Setup Steps

### Step 1: SSH into Your Droplet

```bash
ssh root@<your-droplet-ip>
```

Replace `<your-droplet-ip>` with your DigitalOcean droplet IP address.

### Step 2: Run the Setup Script

```bash
curl -fsSL https://raw.githubusercontent.com/rakirs2/ClinicalTrialData/main/deploy/setup.sh | sudo bash
```

**What this script does:**
1. ✅ Installs PostgreSQL 15
2. ✅ Installs Supervisord (process manager)
3. ✅ Creates the `ct-deploy` user with passwordless sudo
4. ✅ Sets up SSH key-based authentication
5. ✅ Creates `/opt/clinicaltrialdata/` directory structure
6. ✅ Installs Supervisord configurations for 3 services
7. ✅ Starts Supervisord (manages all 3 services)
8. ✅ Services auto-start on droplet reboot

### Step 3: Save the PROD_DB_CONNECTION String

The script output includes:

```
Add this to GitHub secrets as PROD_DB_CONNECTION:

  Host=localhost;Port=5432;Database=clinical_trial_data;Username=postgres;Password=postgres
```

**Copy this string.**

### Step 4: Add to GitHub Secrets

1. Go to your GitHub repository
2. Navigate to **Settings > Secrets and variables > Actions**
3. Click **New repository secret**
4. **Name:** `PROD_DB_CONNECTION`
5. **Value:** Paste the string from Step 3
6. Click **Add secret**

### Step 5: Verify the Setup

SSH into the droplet and verify:

```bash
# Check service status
supervisorctl status

# Expected output:
# clinicaltrialdata-api      RUNNING   pid 1234, uptime 0:00:15
# clinicaltrialdata-frontend RUNNING   pid 1235, uptime 0:00:15
# clinicaltrialdata-ingestion RUNNING  pid 1236, uptime 0:00:15

# Test endpoints
curl http://localhost:5001/       # Frontend
curl http://localhost:5003/health # DataApi
```

### Step 6: Test the Deploy Pipeline

1. Push a commit to `main` branch
2. GitHub Actions automatically deploys
3. Monitor workflow in Actions tab

To watch deployment logs:

```bash
ssh root@<droplet-ip>

# Monitor all supervisor logs
tail -f /var/log/supervisor/clinicaltrialdata-*.log
```

---

## Managing Services

### Check Status

```bash
supervisorctl status
```

Output:
```
clinicaltrialdata-api       RUNNING   pid 1234, uptime 0:05:22
clinicaltrialdata-frontend  RUNNING   pid 1235, uptime 0:05:20
clinicaltrialdata-ingestion RUNNING   pid 1236, uptime 0:05:18
```

### Restart a Service

```bash
# Restart DataApi only
supervisorctl restart clinicaltrialdata-api

# Restart all services
supervisorctl restart clinicaltrialdata-api clinicaltrialdata-frontend clinicaltrialdata-ingestion

# Or restart all at once
supervisorctl restart clinicaltrialdata-*
```

### Stop a Service

```bash
supervisorctl stop clinicaltrialdata-api
```

### Start a Service

```bash
supervisorctl start clinicaltrialdata-api
```

---

## Viewing Logs

### Real-time Logs

```bash
# Watch DataApi logs
tail -f /var/log/supervisor/clinicaltrialdata-api.log

# Watch Frontend logs
tail -f /var/log/supervisor/clinicaltrialdata-frontend.log

# Watch IngestionApp logs
tail -f /var/log/supervisor/clinicaltrialdata-ingestion.log
```

### Last N Lines

```bash
# Show last 50 lines
tail -50 /var/log/supervisor/clinicaltrialdata-api.log

# Show last 100 lines
tail -100 /var/log/supervisor/clinicaltrialdata-api.log
```

### Using supervisorctl

```bash
# View last 100 lines of a service
supervisorctl tail clinicaltrialdata-api

# Follow logs in real-time
supervisorctl tail -f clinicaltrialdata-api
```

---

## Troubleshooting

### Service Won't Start

**Check status:**
```bash
supervisorctl status
```

**View logs:**
```bash
tail -f /var/log/supervisor/clinicaltrialdata-api.log
tail -f /var/log/supervisor/clinicaltrialdata-api-err.log
```

**Common issues:**
- `Connection refused` → PostgreSQL not running: `sudo systemctl status postgresql`
- `Permission denied` → Check directory ownership: `ls -la /opt/clinicaltrialdata/`
- `Address already in use` → Port conflict: `netstat -tlnp | grep 5003`

### Service Keeps Restarting

**Check logs for errors:**
```bash
tail -100 /var/log/supervisor/clinicaltrialdata-api-err.log
```

**View startup attempts:**
```bash
supervisorctl tail -100 clinicaltrialdata-api
```

### Supervisord Won't Start

```bash
# Check if supervisord service is running
systemctl status supervisor

# View supervisor logs
journalctl -u supervisor -f
```

### PostgreSQL Connection Issues

```bash
# Check PostgreSQL is running
sudo systemctl status postgresql

# Test connection manually
psql -h localhost -U postgres -d clinical_trial_data

# Check connection string
cat /etc/clinicaltrialdata.env
```

### Permission Denied Errors

```bash
# Check directory ownership
ls -la /opt/clinicaltrialdata/

# Should show: drwxr-xr-x ct-deploy:ct-deploy

# Verify ct-deploy can write
sudo -u ct-deploy touch /opt/clinicaltrialdata/.test && rm /opt/clinicaltrialdata/.test
```

---

## Directory Structure

```
/opt/clinicaltrialdata/
├── api/                           # DataApi binaries
├── frontend/                       # Frontend binaries
└── ingestion/                      # IngestionApp binaries

/etc/clinicaltrialdata.env          # Environment variables (shared)

/var/log/supervisor/
├── clinicaltrialdata-api.log       # DataApi stdout
├── clinicaltrialdata-api-err.log   # DataApi stderr
├── clinicaltrialdata-frontend.log  # Frontend stdout
├── clinicaltrialdata-frontend-err.log
├── clinicaltrialdata-ingestion.log # IngestionApp stdout
└── clinicaltrialdata-ingestion-err.log

/etc/supervisor/conf.d/
├── clinicaltrialdata-api.conf
├── clinicaltrialdata-frontend.conf
└── clinicaltrialdata-ingestion.conf
```

---

## Manual Deployment (No GitHub Actions)

If you need to manually deploy:

```bash
# 1. Build locally
dotnet publish DataApi/ -c Release -r linux-x64 --self-contained -o publish/dataapi
dotnet publish Frontend/ -c Release -r linux-x64 --self-contained -o publish/frontend
dotnet publish IngestionApp/ -c Release -r linux-x64 --self-contained -o publish/ingestion

# 2. Copy to droplet
scp -r publish/dataapi/* ct-deploy@<droplet-ip>:/opt/clinicaltrialdata/api/
scp -r publish/frontend/* ct-deploy@<droplet-ip>:/opt/clinicaltrialdata/frontend/
scp -r publish/ingestion/* ct-deploy@<droplet-ip>:/opt/clinicaltrialdata/ingestion/

# 3. Restart services
ssh ct-deploy@<droplet-ip> "supervisorctl restart clinicaltrialdata-*"

# 4. Verify
ssh ct-deploy@<droplet-ip> "supervisorctl status"
```

---

## Supervisord Configuration

If you need to modify supervisor settings, edit the config files:

```bash
sudo nano /etc/supervisor/conf.d/clinicaltrialdata-api.conf
```

Then reload:

```bash
supervisorctl reread
supervisorctl update
```

---

## Updating PROD_DB_CONNECTION

If you need to update the database connection string:

```bash
echo "POSTGRES_CONNECTION_STRING=<new-connection-string>" | sudo tee /etc/clinicaltrialdata.env
supervisorctl restart clinicaltrialdata-*
```

---

## Database Backup

Before making changes:

```bash
# Backup
sudo -u postgres pg_dump clinical_trial_data > /home/root/backup.sql

# Restore
sudo -u postgres psql clinical_trial_data < /home/root/backup.sql
```

---

## Support

For issues:

1. Check logs: `tail -f /var/log/supervisor/clinicaltrialdata-*.log`
2. Check status: `supervisorctl status`
3. Check config: `cat /etc/supervisor/conf.d/clinicaltrialdata-*.conf`
4. Check permissions: `ls -la /opt/clinicaltrialdata/`

---

## References

- GitHub Actions Workflow: `.github/workflows/deploy.yml`
- Setup Script: `deploy/setup.sh`
- Supervisor Configs: `deploy/supervisor/`
- Repository Guidelines: `AGENTS.md`
