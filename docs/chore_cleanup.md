# Chore Cleanup Workflow

Standard operating procedure for handling low-risk, frontend-only background tasks.

## Steps

### 1. Audit Open Issues
Query all open GitHub issues. Filter to labels: `chore`, `bug` (frontend-only), or any issue whose scope is limited to `.razor`/`.js`/`.css` files with no API logic changes.

### 2. Select Low-Risk / Frontend-Only Changes
Eligible criteria:
- Only touches files in `Frontend/`
- No new API endpoints or DB queries
- No entity/DB schema changes
- Changes are visual, UX, or rendering fixes
- No new dependencies

### 3. Bundle Into 1 PR
- Branch: `chore/cleanup-YYYYMMDD` from `origin/main`
- One commit per issue, all in the same PR
- PR title: `chore: batch frontend fixes — {summary}`

### 4. Verify Locally
```bash
dotnet build      # 0 errors, 0 warnings
dotnet test       # all pass
```

### 5. Auto-Merge After Tests Pass
- No human review required
- PR merges once CI (`dotnet build` + `dotnet test`) passes
- If tests fail: fix, push, re-trigger CI

### 6. Deploy Without DB Restart
```bash
gh workflow run deploy.yml --ref main
```
Deploy script must NOT reset the database (`--reset-db` flag omitted).

### 7. Verify Against Production
Check each fixed page/component on the live instance:
- Navigate to each affected page
- Confirm the visual/behavioral fix is applied
- Check browser console for JS errors

### 8. Revert on Failure
If any change does not work on production:
- Revert that specific commit: `git revert <commit-sha>`
- Push to main
- Reopen the original issue
- Append a comment with:
  - What was tried
  - Why it failed (screenshot, console error, etc.)
  - Recommended next approach
