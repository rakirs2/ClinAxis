#!/usr/bin/env bash
set -euo pipefail

STUDY_LIMIT="${1:-3000}"

echo "=== Investigator Validation Script ==="
echo "Study limit: $STUDY_LIMIT"
echo ""

# Check Docker
if ! docker info > /dev/null 2>&1; then
    echo "ERROR: Docker is not running. Start Docker Desktop and try again."
    exit 1
fi

cd "$(dirname "$0")/.."

echo "Building Scrapers.Validation..."
dotnet build Scrapers.Validation -q

echo ""
echo "Running validation pipeline..."
dotnet run --project Scrapers.Validation -- "$STUDY_LIMIT"
exit_code=$?

echo ""
if [ $exit_code -eq 0 ]; then
    echo "✓ Validation PASSED"
else
    echo "✗ Validation FAILED — non-human entities found"
    echo "  Fix NameFilter.cs, commit, and re-run this script."
fi

exit $exit_code
