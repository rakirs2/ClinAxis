# Data Loss Remediation Plan

## Summary
The ClinicalTrials.gov scraper was losing **22 fields** from the API response. This document tracks remediation status and prioritizes fixes.

**Current Status:** 7 of 7 categories fixed

The entity model described in `docs/scraper_architecture.md` defines the target state for all persistence. Some fields will be stored as scalar columns on `StudyEntity`; others will use junction tables as documented.

---

## Fields Being Lost (Audit Results)

A comprehensive code audit identified 22 fields from the ClinicalTrials.gov API that were being deserialized by `ClinicalTrialRecord` but never persisted to PostgreSQL. Full technical audit available in `docs/scraper_architecture.md` → "Field Coverage Matrix" section.

---

## By Priority

### PRIORITY 1: Locations (FIXED ✓)
**Status:** COMPLETE in PR #58
- **Fields:** Country, State, City, Facility
- **New table:** `StudyLocationEntity`
- **Impact:** Enables geographic search filtering by country, state, city, facility
- **Effort:** ~30 lines (new entity, migration, mapper update)

---

### PRIORITY 2: Eligibility & Enrollment (FIXED ✓)
**Status:** COMPLETE
- **Fields:** `EligibilityCriteria`, `HealthyVolunteers`
- **Storage:** Scalar fields on `StudyEntity`
- **Effort:** ~20 lines

---

### PRIORITY 3: Sponsorship & Funding (FIXED ✓)
**Status:** COMPLETE
- **Fields:** `LeadSponsorName`, `CollaboratorNames`
- **Storage:** Scalar fields on `StudyEntity`
- **Effort:** ~20 lines

---

### PRIORITY 4: Trial Design Details (FIXED ✓)
**Status:** COMPLETE
- **Fields:** `Masking` (blinding strategy), `OrgStudyId`
- **Storage:** Scalar fields on `StudyEntity`
- **Effort:** ~20 lines

---

### PRIORITY 5: References & Publications (FIXED ✓)
**Status:** COMPLETE in PR #89
- **Fields:** `References[]` array with pmid, citation, type
- **Storage:** Junction table `StudyReferenceEntity`

---

### PRIORITY 6: Trial Outcomes (FIXED ✓)
**Status:** COMPLETE
- **Fields:** `PrimaryOutcomes[]`, `SecondaryOutcomes[]` (measure, description, timeFrame each)
- **Storage:** Junction table `StudyOutcomeEntity` (5 fields: type, measure, description, timeFrame, study_nct_id)
- **Effort:** ~80 lines

---

### PRIORITY 7: Trial Arms (FIXED ✓)
**Status:** COMPLETE
- **Fields:** `ArmGroups[]` array with label, type, description
- **Storage:** Junction table `StudyArmGroupEntity` (4 fields: label, type, description, study_nct_id)
- **Effort:** ~70 lines

---

## Summary Table

| Category | Fields | Status | Table Type | Effort |
|----------|--------|--------|-----------|--------|
| **Locations** | 4 | ✓ FIXED (PR #58) | Junction | 30 lines |
| **Eligibility** | 2 | ✓ FIXED | Scalar | 20 lines |
| **Sponsorship** | 2 | ✓ FIXED | Scalar | 20 lines |
| **Design** | 2 | ✓ FIXED | Scalar | 20 lines |
| **References** | 3 | ✓ FIXED (PR #89) | Junction | 60 lines |
| **Outcomes** | 6 | ✓ FIXED | Junction | 80 lines |
| **Arms** | 3 | ✓ FIXED | Junction | 70 lines |
| **TOTAL** | **22** | **7 Fixed** | **—** | **~360 lines** |

---

## How to Approach Future PRs

Each remediation PR should follow this pattern:

1. **Create new entity classes** (if junction tables are needed)
2. **Create EF Core migration** (add table/columns with foreign keys)
3. **Update `ClinicalTrialRecord` model** (add properties if needed)
4. **Update `StudyRepository.MapRecordToEntity()`** (add mapping logic to populate new fields/tables)
5. **Verify with integration tests** (assert row counts in dependent tables match API data)
6. **Update this plan** (mark category as DONE, link to merged PR)

See `docs/scraper_architecture.md` for the full entity model spec and field coverage matrix.

---

## Design Patterns

### Scalar Fields (Priority 2, 3, 4)
- Add string/bool property directly to `StudyEntity`
- Create migration with `modelBuilder.Entity<StudyEntity>().Property(...)`
- Update mapper: `entity.PropertyName = record.PropertyName`
- Example: `StudyEntity.Masking`, `StudyEntity.LeadSponsorName`

### Junction Tables (Priority 1, 5, 6, 7)
- Create new entity class (e.g., `StudyReferenceEntity`)
- Add `StudyNctId` foreign key and navigation property to `StudyEntity`
- Create migration with `modelBuilder.Entity<StudyReferenceEntity>()` and relationships
- Update mapper with loop: `foreach (var item in record.ItemArray) { entity.Items.Add(...) }`
- Example: PR #58 added `StudyLocationEntity` following this pattern

### Entity Model Alignment
Some fields now belong on new normalized tables rather than `StudyEntity`:
- **References** stored in `StudyReferenceEntity` (replaces per-study PubMed re-fetch)
- **Investigators** stored in normalized `InvestigatorPersonEntity` + `StudyInvestigatorEntity` junction (not duplicated rows)
- See `docs/scraper_architecture.md` → Core Entity Model for full schema

---

## Why Zero Data Loss Matters

**Current Impact:** All 22 fields are now persisted. Geographic search works, study comparison includes sponsor/design/outcome data, references are captured from the API to avoid duplicate PubMed fetches, and study records are complete.

**Long-term:** Each field is part of a public API contract. The API will not expand to include data we discard—we must persist it now or lose it forever.

---

## References

- **PR #58**: Added `StudyLocationEntity` and fixed locations data loss
- **AGENTS.md**: Principle #6 "Zero Data Loss in Scraping" with audit guidance
- **docs/scraper_architecture.md**: Full field coverage matrix and entity model spec

---

## All Data Loss Remediated

All 22 fields from the ClinicalTrials.gov API response are now persisted. No further data loss remediation PRs are needed.
