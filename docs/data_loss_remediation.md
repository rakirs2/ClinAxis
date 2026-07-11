# Data Loss Remediation Plan

## Summary
The ClinicalTrials.gov scraper was losing **22 fields** from the API response. This document tracks remediation status and prioritizes fixes.

**Current Status:** 1 of 7 categories fixed (Locations in PR #58)

---

## Fields Being Lost (Audit Results)

A comprehensive code audit identified 22 fields from the ClinicalTrials.gov API that were being deserialized by `ClinicalTrialRecord` but never persisted to PostgreSQL. Full technical audit available locally in `.opencode/` folder (generated during development).

---

## By Priority

### PRIORITY 1: Locations (FIXED ✓)
**Status:** COMPLETE in PR #58
- **Fields:** Country, State, City, Facility
- **New table:** `StudyLocationEntity`
- **Impact:** Enables geographic search filtering by country, state, city, facility
- **Effort:** ~30 lines (new entity, migration, mapper update)

---

### PRIORITY 2: Eligibility & Enrollment (Next)
**Status:** TODO
- **Fields:** `EligibilityCriteria`, `HealthyVolunteers`
- **Storage:** Add scalar fields to `StudyEntity`
- **Impact:** Users cannot filter studies by enrollment criteria or healthy volunteer status
- **Effort:** ~20 lines (add 2 fields to StudyEntity + migration + mapper)

---

### PRIORITY 3: Sponsorship & Funding (Next)
**Status:** TODO
- **Fields:** `LeadSponsorName`, `CollaboratorNames`
- **Storage:** Add to `StudyEntity` or create new `SponsorEntity`
- **Impact:** Cannot identify primary sponsor or collaborating institutions/funding sources
- **Effort:** ~20 lines (add 2 fields to StudyEntity + migration + mapper)

---

### PRIORITY 4: Trial Design Details (Next)
**Status:** TODO
- **Fields:** `Masking` (blinding strategy), `OrgStudyId`
- **Storage:** Add scalar fields to `StudyEntity`
- **Impact:** Cannot distinguish study design (blinded vs open-label); no organization-level study identifier
- **Effort:** ~20 lines (add 2 fields to StudyEntity + migration + mapper)

---

### PRIORITY 5: References & Publications (Next)
**Status:** TODO
- **Fields:** `References[]` array with pmid, citation, type
- **Storage:** New junction table `StudyReferenceEntity` (3 fields per record)
- **Impact:** Losing links to supporting research publications; PubMed scraper re-fetches what's already in API (duplicate work)
- **Effort:** ~60 lines (new junction table + migration + mapping loop)

---

### PRIORITY 6: Trial Outcomes (Next)
**Status:** TODO
- **Fields:** `PrimaryOutcomes[]`, `SecondaryOutcomes[]` (measure, description, timeFrame each)
- **Storage:** New junction table `StudyOutcomeEntity` (4 fields: type, measure, description, timeFrame)
- **Impact:** Cannot see what success criteria are being measured or expected outcomes
- **Effort:** ~80 lines (new junction table + migration + mapping loop)

---

### PRIORITY 7: Trial Arms (Next)
**Status:** TODO
- **Fields:** `ArmGroups[]` array with label, type, description
- **Storage:** New junction table `StudyArmGroupEntity` (3 fields per record)
- **Impact:** No information about experiment vs control group structure; cannot understand study design
- **Effort:** ~70 lines (new junction table + migration + mapping loop)

---

## Summary Table

| Category | Fields | Status | Table Type | Effort |
|----------|--------|--------|-----------|--------|
| **Locations** | 4 | ✓ FIXED | Junction | 30 lines |
| **Eligibility** | 2 | TODO | Scalar | 20 lines |
| **Sponsorship** | 2 | TODO | Scalar | 20 lines |
| **Design** | 2 | TODO | Scalar | 20 lines |
| **References** | 3 | TODO | Junction | 60 lines |
| **Outcomes** | 6 | TODO | Junction | 80 lines |
| **Arms** | 3 | TODO | Junction | 70 lines |
| **TOTAL** | **22** | **1 Fixed** | **—** | **~360 lines** |

---

## How to Approach Future PRs

Each remediation PR should follow this pattern:

1. **Create new entity classes** (if junction tables are needed)
2. **Create EF Core migration** (add table/columns with foreign keys)
3. **Update `ClinicalTrialRecord` model** (add properties if needed)
4. **Update `StudyRepository.MapRecordToEntity()`** (add mapping logic to populate new fields/tables)
5. **Verify with integration tests** (assert row counts in dependent tables match API data)
6. **Update this plan** (mark category as DONE, link to merged PR)

---

## Design Patterns

### Scalar Fields (Priority 2, 3, 4)
- Add string/bool property directly to `StudyEntity`
- Create migration with `modelBuilder.Entity<StudyEntity>().Property(...)`
- Update mapper: `entity.PropertyName = record.PropertyName`
- Example: PR #58 added `Locations` navigation property

### Junction Tables (Priority 5, 6, 7)
- Create new entity class (e.g., `StudyReferenceEntity`)
- Add `StudyId` foreign key and navigation property to `StudyEntity`
- Create migration with `modelBuilder.Entity<StudyReferenceEntity>()` and relationships
- Update mapper with loop: `foreach (var item in record.ItemArray) { entity.Items.Add(...) }`
- Example: PR #58 added `StudyLocationEntity` following this pattern

---

## Why Zero Data Loss Matters

**Current Impact:** The scraper deserializes all 22 fields from ClinicalTrials.gov API but discards them. This means:
- Geographic search is incomplete (no location filtering)
- Study comparison is blind (no sponsor, design, outcome data)
- Duplicate work (PubMed scraper re-fetches references)
- Incomplete study records (missing critical metadata)

**Long-term:** Each field is part of a public API contract. The API will not expand to include data we discard—we must persist it now or lose it forever.

---

## References

- **PR #58**: Added `StudyLocationEntity` and fixed locations data loss
- **AGENTS.md**: Principle #6 "Zero Data Loss in Scraping" with audit guidance
- **Local docs** (not in git): `.opencode/` folder contains full technical audit with code references and line numbers

---

## Next Agent Instructions

When starting a new data loss remediation PR:
1. Read this file to understand what's left to fix and why
2. Check the "How to Approach Future PRs" section for the pattern
3. Follow PR guidelines in AGENTS.md (one feature per PR, all tests passing)
4. Update this file when your PR is merged (mark category as DONE, add link)
5. For full technical details, refer to audit documents in `.opencode/` folder (locally available during development)
