---
title: EF Bundle Search Path Double-Entry
date: 2026-07-20
tags: [deployment, efbundle, connection-string]
category: deployment
---

## Problem

Deploy failed with:

```
ERROR: schema "publicpublic" does not exist
```

## Root Cause

The `deploy-docker.yml` was appending `;Search Path=public` to the connection string secret, but the secret **already** contained `Search Path=public`. The resulting string had it duplicated:

```
Host=...;Search Path=public;Search Path=public
```

EF Core migration bundles concatenate the two into `"publicpublic"`.

## Fix

Removed `Search Path=public` from `deploy-docker.yml`. The secret is the single source of truth.

## Prevention

Added a grep check to the workflow validation script:

```bash
grep -n "Search Path" .github/workflows/deploy-docker.yml
```

Should return zero matches.

## Cross-references

- [deploy-docker.yml](https://github.com/rakirs2/ClinicalTrialData/blob/main/.github/workflows/deploy-docker.yml)
- See [Rate Limiting](../scrapers/nih-rate-limiting.md) for another "fix it twice" story
