---
title: Hello World
date: 2026-07-28
tags: [meta, getting-started]
category: meta
---

Welcome to the Lessons Learned wiki. This is where we document what went wrong, what went right, and what we'll never do again.

## Why a wiki?

A chronological blog doesn't make sense for institutional knowledge. Issues repeat. Having a searchable, cross-linked wiki means we can find the fix faster next time.

## How to contribute

Drop a `.md` file in the appropriate category subdirectory:

```
blog/
  scrapers/         # NIH, PubMed, CT.gov scraper issues
  deployment/       # Docker, CI/CD, DigitalOcean
  data-quality/     # Data anomalies, missing fields
  meta/             # About this wiki itself
```

Front matter is required:

```yaml
---
title: Your Title Here
date: 2026-07-28
tags: [tag1, tag2]
category: scrapers
---
```

| --- | --- |
| **Tables** | are supported |
| **Code** | `inline` and fenced blocks work |
