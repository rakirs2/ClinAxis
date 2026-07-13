# Scraper Architecture

## 1. Project Vision & Goals

The long-term goal is a website where a user can search by therapeutic type, mechanism of action, location, patient type, etc. and find the **principal investigator (PI) with the highest likelihood of completing a study**.

To enable this, the scraper must:

- **Generate as much relevant data as possible.** Persist every field from every source API — no data loss.
- **Normalize investigators as first-class entities.** Every investigator has a single canonical record with a system-generated GUID, verified identifiers (ORCID, NCBI ID), and a complete publication history.
- **Be event-driven and trackable.** All enrichment steps (investigator publication scrubs, aggregations) are queued as events with claim-process-complete semantics, retry logic, and dead-letter queues.
- **Be coupled to source APIs.** Data enters the system exclusively through automated scrapers. No manual data entry.

---

## 2. Data Sources

### 2.1 ClinicalTrials.gov (Primary)

- **API:** [ClinicalTrials.gov API v2](https://clinicaltrials.gov/api/v2/)
- **Endpoint:** `GET /api/v2/studies` (paginated, batched, up to 100 per page)
- **Authentication:** None (public API)
- **Retry:** Exponential backoff, 3 attempts, transient status detection (429, 503, etc.)
- **Client class:** `Scrapers.ClinicalTrialsGov`
- **Response model:** `StudyListResponse` → `ClinicalTrialRecord`

### 2.2 PubMed / NCBI (Enrichment)

- **API:** NCBI E-utilities (`efetch.fcgi`)
- **Endpoint:** `https://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi?db=pubmed&id={pmid}&retmode=xml`
- **Authentication:** None (public API)
- **Format:** XML
- **Purpose:** Fetch paper details and author metadata (including ORCID) for investigator publication scrubbing

### 2.3 Future Sources (Extensible)

The `IPivotEnricherService` interface in `Scrapers/Services/CrawlServices/` provides a pluggable enricher pattern for future data sources. Each enricher:
- Is auto-discovered via reflection by `PivotServiceRegistry`
- Can be enabled/disabled via the `scraper_pivots` database table
- Enriches individual studies or investigators with additional data

---

## 3. Core Entity Model

The entity model is designed around **investigators as first-class, normalized entities** — not per-study junction rows.

### 3.1 Study Entity (Existing, + scalars for lost fields)

```
StudyEntity (table: studies)
├── NctId (PK, string)
├── BriefTitle, OfficialTitle
├── OverallStatus, StudyType, BriefSummary
├── PrimaryPurpose, InterventionModel, Allocation
├── EnrollmentCount, Sex, MinimumAge, MaximumAge
├── StartDate, CompletionDate, StudyFirstPostDate
├── LeadSponsorName (TODO — data loss fix)
├── EligibilityCriteria (TODO — data loss fix)
├── HealthyVolunteers (TODO — data loss fix)
├── OrgStudyId (TODO — data loss fix)
├── Masking (TODO — data loss fix)
├── CollaboratorNames (TODO — data loss fix)
├── IsIncomplete, CreatedAt
│
├── ICollection<StudyConditionEntity>  → Conditions (junction)
├── ICollection<StudyKeywordEntity>    → Keywords (junction)
├── ICollection<StudyPhaseEntity>      → Phases (junction)
├── ICollection<StudyLocationEntity>   → Locations (junction, FIXED)
├── ICollection<StudyReferenceEntity>  → References (TODO — data loss fix)
├── ICollection<StudyOutcomeEntity>    → Outcomes (TODO — data loss fix)
├── ICollection<StudyArmGroupEntity>   → Arms (TODO — data loss fix)
├── ICollection<StudyInvestigatorEntity> → Investigators (junction, revised)
└── ICollection<PubmedStudyEntity>     → PubMed links (legacy, to be replaced)
```

### 3.2 Investigator Person (NEW — Normalized)

```
InvestigatorPersonEntity (table: investigator_persons)
├── Id (PK, Guid, auto-generated)
├── FullName (string)
├── Orcid (string?, unique)
├── NcbiId (string?, unique)
├── VerifiedAt (DateTime?)           — When identity was verified
├── VerificationSource (string?)     — e.g., "PubMed", "ORCID API"
├── CreatedAt (DateTime)
└── UpdatedAt (DateTime)
```

### 3.3 Investigator Affiliation (NEW — One-to-many from Person)

```
InvestigatorAffiliationEntity (table: investigator_affiliations)
├── Id (PK)
├── InvestigatorPersonId (FK → InvestigatorPersonEntity.Id)
├── InstitutionName (string)
├── Department (string?)
├── City, State, Country (string?)
├── StartDate (DateOnly?)
├── EndDate (DateOnly?)              — null = current
├── Role (string?)                   — e.g., "Professor", "Researcher"
└── IsPrimary (bool)                 — current primary affiliation
```

Supports career history tracking (multiple institutions over time, geographic data per appointment).

### 3.4 Study-Investigator Junction (Revised — replaces current InvestigatorEntity)

```
StudyInvestigatorEntity (table: study_investigators)
├── Id (PK)
├── StudyNctId (FK → StudyEntity)
├── InvestigatorPersonId (FK → InvestigatorPersonEntity)
├── RoleOnStudy (string?)            — e.g., "Principal Investigator", "Sub-Investigator"
├── ContactPhone, ContactEmail (string?) — if available from API
├── IsOverallOfficial (bool)         — true = listed in OverallOfficials array
```

### 3.5 PubMed Paper (NEW — Normalized)

```
PubmedPaperEntity (table: pubmed_papers)
├── Pmid (PK, string)
├── Doi (string?)
├── Title (string?)
├── Journal (string?)
├── PublicationDate (DateTime?)
├── Abstract (string?)
└── IsNonEnglish (bool)
```

Replaces the current per-study `PubmedStudyEntity` with a single row per paper.

### 3.6 Investigator-Paper Junction (NEW)

```
InvestigatorPaperEntity (table: investigator_papers)
├── InvestigatorPersonId (FK → InvestigatorPersonEntity)
├── Pmid (FK → PubmedPaperEntity)
├── AuthorshipRank (int)             — 1 = first author
├── IsCorrespondingAuthor (bool?)
└── PK: (InvestigatorPersonId, Pmid)
```

### 3.7 Study Reference (NEW — Data Loss Fix)

```
StudyReferenceEntity (table: study_references)
├── Id (PK)
├── StudyNctId (FK → StudyEntity)
├── Pmid (string?)
├── Citation (string?)
├── Type (string?)
└── PK: (StudyNctId, Pmid)
```

Stores the `References[]` array from the ClinicalTrials.gov batch response. Eliminates the need for the PubMed scraper to make per-study HTTP calls to re-fetch PMIDs.

### 3.8 Study Outcomes (TODO — Data Loss Fix)

```
StudyOutcomeEntity (table: study_outcomes)
├── Id (PK)
├── StudyNctId (FK → StudyEntity)
├── OutcomeType (string)             — "Primary" or "Secondary"
├── Measure (string?)
├── Description (string?)
└── TimeFrame (string?)
```

### 3.9 Study Arm Groups (TODO — Data Loss Fix)

```
StudyArmGroupEntity (table: study_arm_groups)
├── Id (PK)
├── StudyNctId (FK → StudyEntity)
├── Label (string?)
├── Type (string?)
└── Description (string?)
```

---

## 4. Pipeline Architecture

### Phase 1: Study Ingestion (Batched CT.gov API)

```
┌────────────────────────────────────────────────────────────┐
│ ClinicalTrials.gov API                                     │
│ GET /api/v2/studies (batched, 100/page)                    │
└─────────────────────┬──────────────────────────────────────┘
                      │
                      ▼
┌────────────────────────────────────────────────────────────┐
│ ClinicalTrialsGov client                                   │
│ → Deserialize StudyListResponse → ClinicalTrialRecord[]    │
└─────────────────────┬──────────────────────────────────────┘
                      │ onBatch callback
                      ▼
┌────────────────────────────────────────────────────────────┐
│ ClinicalTrialsIngestionService.IngestAsync()               │
│ Calls StudyRepository.UpdateStudiesWithClinicalTrialsAsync │
└─────────────────────┬──────────────────────────────────────┘
                      │
          ┌───────────┼───────────┐
          │           │           │
          ▼           ▼           ▼
┌──────────────┐ ┌──────────┐ ┌───────────────────────┐
│ Persist      │ │ Create   │ │ Enqueue               │
│ StudyEntity  │ │/match    │ │ "investigator          │
│ + children   │ │Invest-   │ │ .discovered"           │
│ (conditions, │ │igator    │ │ event per              │
│ keywords,    │ │Person    │ │ unique person          │
│ phases,      │ │records   │ │                       │
│ locations,   │ │          │ │                       │
│ references)  │ │          │ │                       │
└──────────────┘ └──────────┘ └───────────────────────┘
```

### Phase 2: Investigator Publication Scrub (Event-Driven)

```
                    ┌──────────────────────┐
                    │ Event Queue           │
                    │ "investigator.discovered"
                    └──────────┬───────────┘
                               │ claim
                               ▼
┌────────────────────────────────────────────────────────────┐
│ EventProcessingService                                     │
│ → Claim next pending event                                 │
│ → Dispatch by event type ("investigator.discovered")       │
└─────────────────────┬──────────────────────────────────────┘
                      │
                      ▼
┌────────────────────────────────────────────────────────────┐
│ InvestigatorPublicationScrubService (NEW)                  │
│ 1. Search PubMed by investigator name + affiliation         │
│ 2. For each PMID not already in PubmedPaperEntity:         │
│    → Fetch paper details from NCBI E-utilities             │
│    → Create PubmedPaperEntity                              │
│    → Create InvestigatorPaperEntity link                   │
│ 3. Mark event complete                                     │
│ 4. On failure → event moves to dead-letter queue           │
└────────────────────────────────────────────────────────────┘
```

### Phase 3: Aggregation (Scheduled)

```
┌────────────────────────────────────────────────────────────┐
│ AggregationService                                         │
│ → Recompute PiAggregationEntity rows                       │
│ → Recompute CategoryAggregationEntity rows                 │
│ (Full delete + replace — currently synchronous)            │
└────────────────────────────────────────────────────────────┘
```

---

## 5. Event System

### 5.1 Event Types

| Event Type | Emitter | Consumer | Description |
|-----------|---------|----------|-------------|
| `studies.discovered` | `ClinicalTrialsScrapeService` | `EventProcessingService` → `ClinicalTrialsIngestionService.IngestAsync` | New study batch fetched from CT.gov |
| `investigator.discovered` | `StudyRepository` (during study ingestion) | `InvestigatorPublicationScrubService` | New investigator person record created |
| `investigator.publications.scrubbed` | `InvestigatorPublicationScrubService` | (future: triggers enrichment pivot) | Investigator's full publication list updated |

### 5.2 Event Lifecycle

```
Enqueue → Pending → Claimed → Completed
                          ↘ Failed → Retry (up to 3, exponential backoff)
                                    ↘ Dead Letter → Manual retry or ignore via DataApi
```

### 5.3 Event Queue Implementation

Backed by the `pipeline_events` database table. The `IEventQueueService` interface provides:
- `EnqueueAsync` — add event
- `ClaimNextPendingEventAsync` — atomic claim (set `claimed_by`, `claimed_at`)
- `CompleteEventAsync` — mark done
- `FailEventAsync` — increment retry, move to dead letter after 3 failures
- `ReleaseStuckEventsAsync` — reclaim events stuck > timeout
- `RetryDeadLetterEventAsync` / `IgnoreDeadLetterEventAsync` — ops intervention

---

## 6. Field Coverage Matrix

All fields returned by the ClinicalTrials.gov API v2 studies endpoint, their persistence status, and storage location.

### 6.1 Persisted Fields

| Module | Field | Storage | Status |
|--------|-------|---------|--------|
| `identificationModule.nctId` | NCT ID | `StudyEntity.NctId` | ✅ |
| `identificationModule.briefTitle` | Brief title | `StudyEntity.BriefTitle` | ✅ |
| `identificationModule.officialTitle` | Official title | `StudyEntity.OfficialTitle` | ✅ |
| `identificationModule.orgStudyIdInfo.id` | Org study ID | `StudyEntity.OrgStudyId` | ❌ TODO |
| `statusModule.overallStatus` | Status | `StudyEntity.OverallStatus` | ✅ |
| `statusModule.startDateStruct` | Start date | `StudyEntity.StartDate` | ✅ |
| `statusModule.completionDateStruct` | Completion date | `StudyEntity.CompletionDate` | ✅ |
| `statusModule.studyFirstPostDateStruct` | First posted | `StudyEntity.StudyFirstPostDate` | ✅ |
| `sponsorCollaboratorsModule.leadSponsor.name` | Lead sponsor | `StudyEntity.LeadSponsorName` | ❌ TODO |
| `sponsorCollaboratorsModule.collaborators[*].name` | Collaborators | `StudyEntity.CollaboratorNames` | ❌ TODO |
| `descriptionModule.briefSummary` | Summary | `StudyEntity.BriefSummary` | ✅ |
| `conditionsModule.conditions` | Conditions | `StudyConditionEntity` | ✅ |
| `conditionsModule.keywords` | Keywords | `StudyKeywordEntity` | ✅ |
| `designModule.studyType` | Study type | `StudyEntity.StudyType` | ✅ |
| `designModule.phases` | Phases | `StudyPhaseEntity` | ✅ |
| `designModule.designInfo.allocation` | Allocation | `StudyEntity.Allocation` | ✅ |
| `designModule.designInfo.interventionModel` | Intervention model | `StudyEntity.InterventionModel` | ✅ |
| `designModule.designInfo.primaryPurpose` | Primary purpose | `StudyEntity.PrimaryPurpose` | ✅ |
| `designModule.designInfo.masking` | Masking | `StudyEntity.Masking` | ❌ TODO |
| `designModule.enrollmentInfo.count` | Enrollment | `StudyEntity.EnrollmentCount` | ✅ |
| `eligibilityModule.eligibilityCriteria` | Criteria | `StudyEntity.EligibilityCriteria` | ❌ TODO |
| `eligibilityModule.sex` | Sex | `StudyEntity.Sex` | ✅ |
| `eligibilityModule.minimumAge` | Min age | `StudyEntity.MinimumAge` | ✅ |
| `eligibilityModule.maximumAge` | Max age | `StudyEntity.MaximumAge` | ✅ |
| `eligibilityModule.healthyVolunteers` | Healthy volunteers | `StudyEntity.HealthyVolunteers` | ❌ TODO |
| `contactsLocationsModule.overallOfficials` | Officials | `StudyInvestigatorEntity` + `InvestigatorPersonEntity` | ✅ (needs revision to normalized model) |
| `contactsLocationsModule.locations[*]` | Locations | `StudyLocationEntity` | ✅ (FIXED) |
| `armsInterventionsModule.armGroups` | Arms | `StudyArmGroupEntity` | ❌ TODO |
| `outcomesModule.primaryOutcomes` | Primary outcomes | `StudyOutcomeEntity` | ❌ TODO |
| `outcomesModule.secondaryOutcomes` | Secondary outcomes | `StudyOutcomeEntity` | ❌ TODO |
| `referencesModule.references[*].pmid` | PMID | `StudyReferenceEntity.Pmid` | ❌ TODO |
| `referencesModule.references[*].citation` | Citation | `StudyReferenceEntity.Citation` | ❌ TODO |
| `referencesModule.references[*].type` | Reference type | `StudyReferenceEntity.Type` | ❌ TODO |

### 6.2 Intentionally Ignored Fields

None. All API fields must be persisted.

---

## 7. Architecture Decisions

### 7.1 Why Normalized Investigator Table

**Decision:** `InvestigatorPersonEntity` with a system-generated GUID, separate from the study-investigator junction.

**Rationale:**
- One canonical record per person, not N rows (one per study)
- Enables tracking ORCID, NCBI ID, and other persistent identifiers on a single record
- Enables full publication history scrubbing per person (not per study)
- The GUID serves as the stable identifier for internal references, URL routing (e.g., `/investigators/{uuid}`), and future cross-referencing with external systems

### 7.2 Why Separate Publication Scrub (Event-Driven)

**Decision:** Investigator publication scrubbing runs as a separate event-driven background service, decoupled from study ingestion.

**Rationale:**
- Study ingestion is fast (just persist API response). Publication scrubbing is slow (multiple API calls per investigator).
- Decoupling lets study ingestion complete quickly; scrubbing catches up asynchronously.
- Scrubbing can be retried per-investigator without re-running the full CT.gov pipeline.
- New investigators discovered later (from future data sources) can be scrubbed independently.

### 7.3 Why Event Queue

**Decision:** All cross-service communication goes through the event queue (`pipeline_events` table).

**Rationale:**
- Claim-process-complete pattern prevents duplicate work (multiple IngestionApp instances).
- Dead-letter queue prevents data loss from transient failures.
- Ops can retry or ignore failed events via DataApi endpoints.
- Full audit trail of all pipeline activity.

### 7.4 Why Single Gateway (DataApi)

**Decision:** Only DataApi reads/writes PostgreSQL. Frontend, IngestionApp, and tests access the database via DataApi REST endpoints or through the shared `Scrapers` library's repositories (which DataApi also uses).

**Rationale:**
- Single contract boundary: all consumers negotiate one API surface.
- Independent scaling: DataApi can be scaled separately from Frontend/Ingestion.
- Prevents tight coupling: Frontend never needs to know about database schema.

**Current exception:** `PubMedScraperService` creates its own `ClinicalTrialsContext` directly. This violates the single-gateway rule and will be fixed in a future PR.

### 7.5 Why No Nginx

**Decision:** Kestrel serves HTTPS directly on localhost ports (5001, 5003). No nginx reverse proxy.

**Rationale:**
- Simpler stack (fewer moving parts).
- .NET's built-in Kestrel is production-ready.
- Services are internal (localhost-only binding); no public-facing ports.

### 7.6 Why Affiliations Are a Separate Table (Not a Column)

**Decision:** `InvestigatorAffiliationEntity` is a one-to-many child of `InvestigatorPersonEntity`.

**Rationale:**
- Investigators change institutions over their career. A single column would lose history.
- Geographic data (city, state, country) per appointment enables location-based search.
- Start/end dates enable timeline queries (e.g., "which institution was this PI at when this study started?").

---

## 8. Test Strategy

### 8.1 Three Database Testing Modes

| Mode | Class | Use Case |
|------|-------|----------|
| **Integration/IO** | `DbTestBase` | Testcontainers container per class, transaction rollback per method. Fast and isolated. |
| **Snapshot** | `SnapshotDb` | Container seeded with known golden data. Deterministic assertions. |
| **Persistent/fiddle** | `SnapshotDb(persist: true)` | Same as snapshot but no rollback — DB stays for manual inspection. |

### 8.2 Per-API Test Coverage

| Test Type | Class Pattern | Example |
|-----------|--------------|---------|
| **Unit** (fake HTTP with captured payloads) | `*Tests.cs` | `ClinicalTrialsGovClientTests`, `PubMedScraperServiceTests` |
| **Live API smoke** | `*IntegrationTests.cs` | `ClinicalTrialsGovIntegrationTests` |
| **Schema guard** (JSON shape validation) | `*SchemaGuardTests.cs` | `DataApiSchemaGuardTests` |

### 8.3 Data Loss Verification

Every scraper integration test must verify:
- Row counts in dependent tables match API data (e.g., if API returns 3 locations, assert `study_locations` has 3 rows for that study)
- No data is silently dropped during mapping
- Full field coverage is tested via assertions or schema guards

### 8.4 Test Utilities

Live in `Scrapers/Testing/`, shared via `InternalsVisibleTo`. Never duplicated.

---

## 9. PR Sequence

| PR | Scope | Description |
|----|-------|-------------|
| **1** | Study References | Create `StudyReferenceEntity` + migration. Store `References[]` from batch response. Eliminate per-study CT.gov HTTPS call in PubMedScraper. |
| **2** | Normalized Investigators | Create `InvestigatorPersonEntity`, `InvestigatorAffiliationEntity`, `StudyInvestigatorEntity`. Migration. Mapper updates. Retroactive deduplication. |
| **3** | Normalized PubMed Papers | Create `PubmedPaperEntity`, `InvestigatorPaperEntity`. Migration to migrate existing `PubmedStudyEntity` data. Update PubMedScraper. |
| **4** | Investigator Publication Scrub | New background service: search PubMed by name+affiliation, fetch all papers, link via junction. Event-driven from `investigator.discovered`. |
| **5** | Scalar Data Loss Fixes | `EligibilityCriteria`, `HealthyVolunteers`, `LeadSponsorName`, `CollaboratorNames`, `OrgStudyId`, `Masking` as scalar columns on `StudyEntity`. |
| **6** | Junction Data Loss Fixes | `StudyOutcomeEntity`, `StudyArmGroupEntity` as junction tables. Migrations. Mapper updates. |
