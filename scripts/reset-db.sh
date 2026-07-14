#!/bin/bash
# Resets the Clinical Trial Data database for a fresh run.
# Usage: ./scripts/reset-db.sh [connection_string]
#
# Default connection string uses postgres/postgres (local dev default).
# Pass a custom connection string as the first argument for non-default setups.
#
# Examples:
#   ./scripts/reset-db.sh
#   ./scripts/reset-db.sh "Host=localhost;Port=5432;Database=clinical_trial_data;Username=myuser;Password=mypass"

CONNECTION_STRING="${1:-Host=localhost;Port=5432;Database=clinical_trial_data;Username=postgres;Password=postgres}"

echo "Resetting database..."
POSTGRES_CONNECTION_STRING="$CONNECTION_STRING" dotnet run --project DataApi/ -- --reset-db

echo "Done."
