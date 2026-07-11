#!/bin/bash
# scripts/validate-workflows.sh
# Validates all GitHub Actions workflow YAML files using actionlint

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"

# Check if actionlint is installed
if ! command -v actionlint &> /dev/null; then
  echo "❌ actionlint not found. Install with: brew install actionlint"
  exit 1
fi

echo "🔍 Validating GitHub Actions workflows..."
echo ""

WORKFLOW_DIR="$REPO_ROOT/.github/workflows"
ERRORS=0

# Find all YAML files in workflows directory
if [ ! -d "$WORKFLOW_DIR" ]; then
  echo "❌ Workflow directory not found: $WORKFLOW_DIR"
  exit 1
fi

# Validate each workflow file
for workflow_file in "$WORKFLOW_DIR"/*.yml; do
  if [ -f "$workflow_file" ]; then
    echo "Validating: $(basename "$workflow_file")..."
    
    if actionlint "$workflow_file" > /dev/null 2>&1; then
      echo "  ✅ PASS"
    else
      echo "  ❌ FAIL"
      actionlint "$workflow_file"
      ERRORS=$((ERRORS + 1))
    fi
  fi
done

echo ""
if [ $ERRORS -eq 0 ]; then
  echo "✅ All workflows valid!"
  exit 0
else
  echo "❌ $ERRORS workflow(s) failed validation"
  exit 1
fi
