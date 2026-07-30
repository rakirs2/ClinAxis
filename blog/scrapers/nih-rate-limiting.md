---
title: NIH ClinicalTrials.gov Rate Limiting
date: 2026-07-25
tags: [rate-limiting, nih, retry]
category: scrapers
---

## Problem

The NIH ClinicalTrials.gov API started returning `429 Too Many Requests` after ~50 concurrent requests during the initial scrape. No retry logic existed, so the scraper failed silently and we lost data.

## Root Cause

The scraper launched all study detail requests in parallel without throttling. NIH's API has a soft limit of ~10 requests/second per IP.

## Fix

Added an exponential backoff retry handler with jitter:

| Attempt | Wait time |
|---------|-----------|
| 1st retry | 1s |
| 2nd retry | 2s |
| 3rd retry | 4s |
| 4th+ retry | 8s (cap) |

Also added a `SemaphoreSlim(10, 10)` to cap concurrent requests.

## Verification

Ran a full re-scrape — zero 429s. Asserted in integration tests by injecting a `FakeHttpMessageHandler` that returns 429 on the first call.

## Cross-references

- See [efbundle search path](../deployment/efbundle-search-path.md) for deployment gotchas
