#!/bin/bash
# Validate all GitHub Actions workflow files
# 
# Checks:
# 1. actionlint compliance (YAML syntax + GitHub Actions specific rules)
# 2. Line length (max 120 characters)
# 3. Indentation consistency (2-space per .editorconfig)
# 4. sudo permissions — workflows must use 'sudo tee' or 'sudo -c' for /etc/ writes

set -e

WORKFLOWS_DIR=".github/workflows"
MAX_LINE_LENGTH=120
ERRORS=0
WARNINGS=0

echo "=== GitHub Actions Workflow Validation ==="
echo ""

# Check if actionlint is installed
if ! command -v actionlint &> /dev/null; then
    echo "❌ actionlint not found. Install with: brew install actionlint"
    exit 1
fi

echo "Checking actionlint compliance..."
for file in "$WORKFLOWS_DIR"/*.yml; do
    if [ -f "$file" ]; then
        echo -n "  $(basename "$file"): "
        if actionlint "$file" > /dev/null 2>&1; then
            echo "✓ Pass"
        else
            echo "✗ Fail"
            actionlint "$file"
            ERRORS=$((ERRORS + 1))
        fi
    fi
done

echo ""
echo "Checking line lengths (max $MAX_LINE_LENGTH chars)..."
for file in "$WORKFLOWS_DIR"/*.yml; do
    if [ -f "$file" ]; then
        violations=$(awk -v max="$MAX_LINE_LENGTH" 'length($0) > max {print NR": "length($0)" chars"}' "$file")
        if [ -n "$violations" ]; then
            echo "  $(basename "$file"): ✗ Violations found:"
            echo "$violations" | sed 's/^/    /'
            ERRORS=$((ERRORS + 1))
        else
            echo "  $(basename "$file"): ✓ Pass"
        fi
    fi
done

echo ""
echo "Checking sudo permissions in workflows..."
for file in "$WORKFLOWS_DIR"/*.yml; do
    if [ -f "$file" ]; then
        # Check for patterns that indicate file writes without proper sudo escalation
        # Pattern: 'cat >' or 'echo >' without 'sudo', particularly for /etc/ paths
        problematic=$(grep -n "cat > /etc\|echo > /etc" "$file" 2>/dev/null | grep -v "sudo\|tee" || true)
        
        if [ -n "$problematic" ]; then
            echo "  $(basename "$file"): ✗ Potential permission issues found:"
            echo "$problematic" | sed 's/^/    /'
            echo "    → Use 'sudo tee' or 'sudo -c' for /etc/ writes"
            ERRORS=$((ERRORS + 1))
        else
            echo "  $(basename "$file"): ✓ Pass"
        fi
    fi
done

echo ""
echo "Checking indentation consistency..."
for file in "$WORKFLOWS_DIR"/*.yml; do
    if [ -f "$file" ]; then
        # Check for lines with odd indentation (not multiple of 2)
        bad_indent=$(awk 'NF && /^[[:space:]]+/ {
            indent = match($0, /[^ ]/) - 1
            if (indent % 2 != 0) print NR": "indent" spaces (odd indentation)"
        }' "$file" | head -5)
        
        if [ -n "$bad_indent" ]; then
            echo "  $(basename "$file"): ✗ Indentation issues found:"
            echo "$bad_indent" | sed 's/^/    /'
            ERRORS=$((ERRORS + 1))
        else
            echo "  $(basename "$file"): ✓ Pass"
        fi
    fi
done

echo ""
if [ $ERRORS -eq 0 ]; then
    echo "✅ All validations passed"
    exit 0
else
    echo "❌ $ERRORS validation(s) failed"
    exit 1
fi
