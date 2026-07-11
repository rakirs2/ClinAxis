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

# Create app directory
echo "Creating application directory..."
mkdir -p /var/www/ct-data
chown -R ct-deploy:ct-deploy /var/www/ct-data

# Create environment file
echo "Creating environment file..."
mkdir -p /etc
cat > /etc/ct-data-api.env <<EOF
POSTGRES_CONNECTION_STRING=Host=localhost;Port=5432;Database=clinical_trial_data;Username=postgres;Password=postgres
EOF
chmod 600 /etc/ct-data-api.env
chown root:root /etc/ct-data-api.env

# Install systemd units
echo "Installing systemd units..."
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cp "$SCRIPT_DIR/systemd/ct-data-api.service" /etc/systemd/system/
cp "$SCRIPT_DIR/systemd/ct-frontend.service" /etc/systemd/system/
systemctl daemon-reload

# Enable services to auto-start
systemctl enable ct-data-api > /dev/null 2>&1
systemctl enable ct-frontend > /dev/null 2>&1

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
echo "  - ct-data-api (port 5003, localhost only)"
echo "  - ct-frontend (port 80)"
echo ""
echo "Deploy user: ct-deploy"
echo "App directory: /var/www/ct-data/"
echo ""
echo "Next steps:"
echo "  1. Copy the PROD_DB_CONNECTION string above"
echo "  2. Add it to GitHub secrets (Settings > Secrets > Actions)"
echo "  3. Merge PR 5 to main"
echo "  4. GitHub Actions will deploy automatically"
echo ""
echo "=========================================="
