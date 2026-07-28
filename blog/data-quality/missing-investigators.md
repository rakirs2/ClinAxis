---
title: Missing Investigators in Production Data
date: 2026-07-18
tags: [data-quality, investigators, mapping]
category: data-quality
---

## Problem

~12% of studies in production had `null` or empty investigators, even though ClinicalTrials.gov returned investigator data for those NCT IDs.

## Root Cause

The API response has two possible investigator locations:

| Location | Example | Coverage |
|----------|---------|----------|
| `study.overallOfficial` | Single PI | ~60% |
| `study.conditionBrowse` | Investigator list | ~28% |
| Both | Duplicated | ~12% |

A bug in the mapper checked the wrong JSON path for one of these, producing a silent null.

The actual root cause: **no integration test** verified investigator row counts matched the API response. Unit tests passed because they tested with hardcoded fixtures that happened to match the buggy path.

## Fix

1. Fixed the JSON path in `StudyMapper.cs`
2. Added integration test asserting `investigator_persons` row count matches parsed API data
3. Added a schema guard test that fails if the API response shape changes

## Verification

Before fix: 88% investigator coverage. After fix: 100%.
