#!/bin/bash
set -e

echo "=========================================="
echo "Clinical Trial Data — Droplet Setup"
echo "=========================================="
echo ""

# Add PostgreSQL official repository
echo "Adding PostgreSQL repository..."
sh -c 'echo "deb http://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" > /etc/apt/sources.list.d/pgdg.list'
wget --quiet -O - https://www.postgresql.org/media/keys/ACCC4CF8.asc | apt-key add - > /dev/null 2>&1

# Update and install dependencies
echo "Updating system packages..."
apt-get update > /dev/null 2>&1
apt-get install -y postgresql-15 postgresql-contrib-15 > /dev/null 2>&1

# Start PostgreSQL
echo "Starting PostgreSQL..."
systemctl start postgresql
systemctl enable postgresql > /dev/null 2>&1

# Create database and user
echo "Creating database and user..."
sudo -u postgres psql <<EOF
CREATE DATABASE clinical_trial_data;
CREATE USER postgres WITH PASSWORD 'postgres';
GRANT ALL PRIVILEGES ON DATABASE clinical_trial_data TO postgres;
ALTER DATABASE clinical_trial_data OWNER TO postgres;
EOF

# Create deploy user
echo "Creating deploy user..."
useradd -m -s /bin/bash ct-deploy 2>/dev/null || true

# Configure passwordless sudo for ct-deploy
echo "Configuring sudo for ct-deploy..."
echo "ct-deploy ALL=(ALL) NOPASSWD: ALL" | sudo tee /etc/sudoers.d/ct-deploy > /dev/null
chmod 440 /etc/sudoers.d/ct-deploy

# Create SSH key directory and add public key
echo "Setting up SSH for ct-deploy..."
mkdir -p /home/ct-deploy/.ssh
echo "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAICKnMxPGGAc7sqOA2nQmF0N0jX6BCbJvE/2Ln8pSfskX ct-deploy@157.245.250.196" >> /home/ct-deploy/.ssh/authorized_keys
chmod 700 /home/ct-deploy/.ssh
chmod 600 /home/ct-deploy/.ssh/authorized_keys
chown -R ct-deploy:ct-deploy /home/ct-deploy/.ssh

# Clean up old deployment directories
echo "Cleaning up old deployment infrastructure..."
systemctl stop ct-data-api ct-frontend 2>/dev/null || true
systemctl disable ct-data-api ct-frontend 2>/dev/null || true
rm -f /etc/systemd/system/ct-data-api.service /etc/systemd/system/ct-frontend.service 2>/dev/null || true
rm -rf /var/www/ct-data 2>/dev/null || true

# Create new app directory with proper permissions
echo "Creating application directory structure..."
mkdir -p /opt/clinicaltrialdata/{api,frontend,ingestion}
chown -R ct-deploy:ct-deploy /opt/clinicaltrialdata
chmod -R 755 /opt/clinicaltrialdata

# Verify permissions
echo "Verifying ct-deploy can write to app directory..."
sudo -u ct-deploy touch /opt/clinicaltrialdata/.deploy-test && rm /opt/clinicaltrialdata/.deploy-test
sudo -u ct-deploy touch /opt/clinicaltrialdata/api/.deploy-test && rm /opt/clinicaltrialdata/api/.deploy-test
sudo -u ct-deploy touch /opt/clinicaltrialdata/frontend/.deploy-test && rm /opt/clinicaltrialdata/frontend/.deploy-test
sudo -u ct-deploy touch /opt/clinicaltrialdata/ingestion/.deploy-test && rm /opt/clinicaltrialdata/ingestion/.deploy-test

# Create environment file
echo "Creating environment file..."
mkdir -p /etc
cat > /etc/clinicaltrialdata.env <<EOF
POSTGRES_CONNECTION_STRING=Host=localhost;Port=5432;Database=clinical_trial_data;Username=postgres;Password=postgres;Maximum Pool Size=25;Connection Idle Lifetime=300;Connection Pruning Interval=60
EOF
chmod 600 /etc/clinicaltrialdata.env
chown root:root /etc/clinicaltrialdata.env

# Install systemd units
echo "Installing systemd units..."
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cp "$SCRIPT_DIR/systemd/clinicaltrialdata-api.service" /etc/systemd/system/
cp "$SCRIPT_DIR/systemd/clinicaltrialdata-frontend.service" /etc/systemd/system/
cp "$SCRIPT_DIR/systemd/clinicaltrialdata-ingestion.service" /etc/systemd/system/
systemctl daemon-reload

# Enable services to auto-start
echo "Enabling services for auto-start..."
systemctl enable clinicaltrialdata-api > /dev/null 2>&1
systemctl enable clinicaltrialdata-frontend > /dev/null 2>&1
systemctl enable clinicaltrialdata-ingestion > /dev/null 2>&1

# Print summary
PROD_DB_CONNECTION="Host=localhost;Port=5432;Database=clinical_trial_data;Username=postgres;Password=postgres"

echo ""
echo "=========================================="
echo "Setup complete!"
echo "=========================================="
echo ""
echo "Add this to GitHub secrets as PROD_DB_CONNECTION:"
echo ""
echo "  $PROD_DB_CONNECTION"
echo ""
echo "Services installed:"
echo "  - clinicaltrialdata-api (port 5003, localhost only)"
echo "  - clinicaltrialdata-frontend (port 5001)"
echo "  - clinicaltrialdata-ingestion (background service)"
echo ""
echo "Deploy user: ct-deploy"
echo "App directory: /opt/clinicaltrialdata/"
echo ""
echo "Next steps:"
echo "  1. Copy the PROD_DB_CONNECTION string above"
echo "  2. Add it to GitHub secrets (Settings > Secrets > Actions)"
echo "  3. Merge the setup PR to main"
echo "  4. GitHub Actions will deploy automatically"
echo ""
echo "Verify deployment:"
echo "  sudo systemctl status clinicaltrialdata-*"
echo "  curl http://localhost:5001/"
echo "  curl http://localhost:5003/health"
echo ""
echo "=========================================="
