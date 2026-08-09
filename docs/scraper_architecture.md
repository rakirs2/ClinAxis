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

New data sources are added as enrichment services (e.g., `MedicareUtilizationService`, `OpenPaymentsService`, `InvestigatorMetricsService`) wired directly in `IngestionApp/Program.cs`. An earlier pluggable pivot-enricher framework (`IPivotEnricherService` + `PivotServiceRegistry` + `scraper_pivots` table) was removed in 2026-08 as dead code — it had no implementations and no consumers.

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
├── LeadSponsorName
├── EligibilityCriteria
├── HealthyVolunteers
├── OrgStudyId
├── Masking
├── CollaboratorNames
├── IsIncomplete, CreatedAt
│
├── ICollection<StudyConditionEntity>  → Conditions (junction)
├── ICollection<StudyKeywordEntity>    → Keywords (junction)
├── ICollection<StudyPhaseEntity>      → Phases (junction)
├── ICollection<StudyLocationEntity>   → Locations (junction, FIXED)
├── ICollection<StudyReferenceEntity>  → References
├── ICollection<StudyOutcomeEntity>    → Outcomes
├── ICollection<StudyArmGroupEntity>   → Arms
├── ICollection<StudyInvestigatorEntity> → Investigators (junction, revised)
└── ICollection<PubmedStudyEntity>     → PubMed links (legacy, to be replaced)
```

### 3.2 Investigator Person (NEW — Normalized)

```
InvestigatorPersonEntity (table: investigator_persons)
├── Id (PK, Guid, auto-generated)
├── FullName (string)
├── Prefix (string?)                 — honorific (e.g., "Dr.")
├── Orcid (string?, unique)
├── NcbiId (string?, unique)
├── Npi (string?, unique)            — National Provider Identifier
├── IsHuman (bool)                   — name filter classification
├── NpiLookupAttemptedAt (DateTime?) — when NPPES query was last attempted
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

### 3.8 Study Outcomes

```
StudyOutcomeEntity (table: study_outcomes)
├── Id (PK)
├── StudyNctId (FK → StudyEntity)
├── OutcomeType (string)             — "Primary" or "Secondary"
├── Measure (string?)
├── Description (string?)
└── TimeFrame (string?)
```

### 3.9 Study Arm Groups

```
StudyArmGroupEntity (table: study_arm_groups)
├── Id (PK)
├── StudyNctId (FK → StudyEntity)
├── Label (string?)
├── Type (string?)
└── Description (string?)
```

### 3.10 Person Identifier Candidate (NEW — NPI Enrichment)

```
PersonIdentifierCandidateEntity (table: person_identifier_candidates)
├── Id (PK, Guid)
├── PersonId (FK → InvestigatorPersonEntity)
├── IdentifierType (string)          — "NPI"
├── IdentifierValue (string)
├── SourceName (string)              — "NPPES"
├── MatchedFullName (string?)
├── MatchedAffiliation (string?)
├── MatchedState (string?)
├── SourceStatus (string?)           — "A" (active) or "D" (deactivated)
├── SourceDeactivatedAt (DateTime?)
├── IsAutoApproved (bool)            — auto-assigned to person.Npi
├── IsResolved (bool)                — manually reviewed
└── CreatedAt (DateTime)
```

Stores every API result from NPPES NPI Registry during enrichment, regardless of whether it was auto-assigned. When exactly 1 active match is found, `IsAutoApproved` is set to `true` and `InvestigatorPersonEntity.Npi` is populated. If 0 or 2+ matches, candidates are preserved for manual review.

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
│ + children   │ │Invest-   │ │ .enrichment"           │
│ (conditions, │ │igator    │ │ event per              │
│ keywords,    │ │Person    │ │ unique person          │
│ phases,      │ │records   │ │                       │
│ locations,   │ │          │ │                       │
│ references)  │ │          │ │                       │
└──────────────┘ └──────────┘ └───────────────────────┘
```

### Phase 1.5: Investigator NPI Enrichment (Event-Driven)

```
                    ┌──────────────────────┐
                    │ Event Queue           │
                    │ "investigator.enrichment"
                    └──────────┬───────────┘
                               │ claim (every 30s)
                               ▼
┌────────────────────────────────────────────────────────────┐
│ InvestigatorEnrichmentService                               │
│ 1. Query NPPES NPI Registry by first/last + affiliation     │
│ 2. Exactly 1 active match → auto-assign NPI to person      │
│    0 or 2+ matches → store candidates in                   │
│    PersonIdentifierCandidateEntity for review               │
│ 3. Set NpiLookupAttemptedAt (prevents re-query)            │
│ 4. Enqueue "investigator.discovered" for next phase        │
│ 5. Mark enrichment event complete                          │
│ 6. On API failure → event retries (up to 3, then dead-letter)
└─────────────────────┬──────────────────────────────────────┘
                       │ enqueue
                       ▼
```

### Phase 2: Investigator Publication Scrub (Event-Driven)

```
                    ┌──────────────────────┐
                    │ Event Queue           │
                    │ "investigator.discovered"
                    │ ← from enrichment service
                    └──────────┬───────────┘
                               │ claim
                               ▼
┌────────────────────────────────────────────────────────────┐
│ InvestigatorPublicationScrubService                         │
│ 1. Search PubMed by investigator name + affiliation         │
│    (NPI may be used for disambiguation if available)        │
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
│ (Aggregation phase removed 2026-08 — AggregationService,   │
│ pi_aggregations, category_aggregations were dead code:     │
│ only reachable via the un-called PipelineRunner.           │
│ PI/category stats are computed on demand by DataApi.)      │
└────────────────────────────────────────────────────────────┘
```

---

## 5. Event System

### 5.1 Event Types

| Event Type | Emitter | Consumer | Description |
|-----------|---------|----------|-------------|
| `studies.discovered` | `ClinicalTrialsScrapeService` | `EventProcessingService` → `ClinicalTrialsIngestionService.IngestAsync` | New study batch fetched from CT.gov |
| `investigator.enrichment` | `StudyRepository` (during study ingestion) | `InvestigatorEnrichmentService` | New person created, needs NPI lookup |
| `investigator.discovered` | `InvestigatorEnrichmentService` | `InvestigatorPublicationScrubService` | NPI enrichment done, ready for PubMed scrub |
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
| `identificationModule.orgStudyIdInfo.id` | Org study ID | `StudyEntity.OrgStudyId` | ✅ |
| `statusModule.overallStatus` | Status | `StudyEntity.OverallStatus` | ✅ |
| `statusModule.startDateStruct` | Start date | `StudyEntity.StartDate` | ✅ |
| `statusModule.completionDateStruct` | Completion date | `StudyEntity.CompletionDate` | ✅ |
| `statusModule.studyFirstPostDateStruct` | First posted | `StudyEntity.StudyFirstPostDate` | ✅ |
| `sponsorCollaboratorsModule.leadSponsor.name` | Lead sponsor | `StudyEntity.LeadSponsorName` | ✅ |
| `sponsorCollaboratorsModule.collaborators[*].name` | Collaborators | `StudyEntity.CollaboratorNames` | ✅ |
| `descriptionModule.briefSummary` | Summary | `StudyEntity.BriefSummary` | ✅ |
| `conditionsModule.conditions` | Conditions | `StudyConditionEntity` | ✅ |
| `conditionsModule.keywords` | Keywords | `StudyKeywordEntity` | ✅ |
| `designModule.studyType` | Study type | `StudyEntity.StudyType` | ✅ |
| `designModule.phases` | Phases | `StudyPhaseEntity` | ✅ |
| `designModule.designInfo.allocation` | Allocation | `StudyEntity.Allocation` | ✅ |
| `designModule.designInfo.interventionModel` | Intervention model | `StudyEntity.InterventionModel` | ✅ |
| `designModule.designInfo.primaryPurpose` | Primary purpose | `StudyEntity.PrimaryPurpose` | ✅ |
| `designModule.designInfo.masking` | Masking | `StudyEntity.Masking` | ✅ |
| `designModule.enrollmentInfo.count` | Enrollment | `StudyEntity.EnrollmentCount` | ✅ |
| `eligibilityModule.eligibilityCriteria` | Criteria | `StudyEntity.EligibilityCriteria` | ✅ |
| `eligibilityModule.sex` | Sex | `StudyEntity.Sex` | ✅ |
| `eligibilityModule.minimumAge` | Min age | `StudyEntity.MinimumAge` | ✅ |
| `eligibilityModule.maximumAge` | Max age | `StudyEntity.MaximumAge` | ✅ |
| `eligibilityModule.healthyVolunteers` | Healthy volunteers | `StudyEntity.HealthyVolunteers` | ✅ |
| `contactsLocationsModule.overallOfficials` | Officials | `StudyInvestigatorEntity` + `InvestigatorPersonEntity` | ✅ (needs revision to normalized model) |
| `contactsLocationsModule.locations[*]` | Locations | `StudyLocationEntity` | ✅ (FIXED) |
| `armsInterventionsModule.armGroups` | Arms | `StudyArmGroupEntity` | ✅ |
| `outcomesModule.primaryOutcomes` | Primary outcomes | `StudyOutcomeEntity` | ✅ |
| `outcomesModule.secondaryOutcomes` | Secondary outcomes | `StudyOutcomeEntity` | ✅ |
| `referencesModule.references[*].pmid` | PMID | `StudyReferenceEntity.Pmid` | ✅ |
| `referencesModule.references[*].citation` | Citation | `StudyReferenceEntity.Citation` | ✅ |
| `referencesModule.references[*].type` | Reference type | `StudyReferenceEntity.Type` | ✅ |

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

### 7.7 Keyword Acceptance: Acronym Expansion + Design-Descriptor Allowlist + MeSH Gate (issue #343/#355)

**Decision:** Keywords are accepted when any of these hold:

1. **Acronym expansion (issue #355):** whole-keyword acronyms (`MI`, `CVA`, `DKA`, `PE`, `DVT`, `ARDS`, `MRSA`, `AF`, `HF`, …) are expanded to their canonical medical term *before* the length and blocklist rules. The expanded term is what is persisted (`study_keywords`) and evaluated against MeSH (exact descriptor match ≈ 1.0, vs. the raw acronym's < 0.65 for the weakest — `CVA` 0.54, `DKA` 0.61). A few raw acronyms now cross the threshold with the BioBERT model (`MI` 0.71, `COPD` 0.72, `AF` 0.79) and are rescued by the gate even unexpanded. Both raw and expanded forms are recorded in `rejected_terms`, so acceptance is analyzable.
2. **Design-descriptor allowlist (issue #343):** trial-design/phase descriptors (randomised controlled trial, pilot study, open label, …) bypass the junk blocklist.
3. **MeSH gate (issue #343):** structural rejections (short/odd tokens) that match a MeSH descriptor at ≥ 0.65 are kept. The threshold was re-picked for the biomedical SBERT upgrade (issue #355 P4-e): `all-MiniLM-L6-v2` → `pritamdeka/S-BioBert-snli-multinli-stsb` (384 → 768 dim, cased tokenizer, `normalize_embeddings=True`). On the 139-keyword labeled set, 0.8 kept only 50.4% of legitimate keywords; 0.65 (the distribution knee) keeps 85.6% while every non-descriptor junk sample scores < 0.65. Generic junk (`treatment`, `safety`, `efficacy`, `patient`, …) is never rescued — the acceptance criteria require it to stay rejected even when it scores ≥ 0.65 against the model (junk that IS a MeSH descriptor, e.g. `Safety`, scores ~1.0 and is kept out by the blocklist, not the threshold).

**Rationale:** The CT.gov keyword field is a mixture of genuine condition descriptors and study-design noise. Filtering only by a static blocklist silently dropped valid acronyms and design terms; the layered acceptance keeps the junk out while preserving real signal. Acronym expansion is stored (not just matched) so keyword search and aggregation operate on canonical terms; the raw form remains reconstructible from the expansion map.

**Single matcher (P4 step ⑤, PR #444):** BERT is the single matcher gating acceptance — the rule-based Side A (`IsValidConditionSimple`, `side_a_valid`) was removed after the A/B sampling read path (PR #440) showed the gate was already BERT-only (`Accepted => SideBMatched`). `rejected_terms` now records only BERT outcomes (`side_b_matched`, `side_b_mesh_term`, `side_b_similarity`, `accepted`), and the DataQuality Keyword Gate tab surfaces the similarity-band distribution for false-reject-rate sampling.

**Intentionally ignored (documented):** keywords are only processed when the record has `overallOfficials` (pre-existing `if (!incomplete)` skip); the MeSH model is cased (BioBERT), so `MeSHMatcher` tokenizes case-sensitively and the memo-cache keys are case-sensitive (`MI` ≠ `mi`).

### 7.8 Location Normalization (issue #380)

**Decision:** At ingest, `StudyRepository` normalizes each `study_locations` row via the pure `LocationNormalizer` helper: country aliases → canonical name (`U.S.A.`/`USA`/`America` → `United States`, `UK`/`Great Britain` → `United Kingdom`), US state full names → 2-letter codes (`Maryland` → `MD`), and all fields trimmed with inner whitespace collapsed. Normalized values also feed `LocationMeshMatcher.Match`, so `U.S.A.` now resolves to the `United States` Z-geographical MeSH descriptor.

**Rationale:** Location fields are raw free-text from CT.gov; canonical forms make distinct-location counts, search facets (#27/#30), and geo weighting (#170) tractable. Replacing the raw string with the canonical form is intentional normalization, not data loss — the original remains reconstructible from the alias maps for the common cases.

**Deferred (documented):** production distinct-location audit (step 1), lat/lon geocoding spike (step 3), and the DataQuality coverage column (step 5) — coordinates and geo search are gated on #170/#27/#30.

---

## 8. Operational Table Inventory (issue #351)

Decisions from the 2026-08 dead-table audit (commit `feature/drop-dead-tables`).

| Table | Writer | Reader / surfaced | Status |
|-------|--------|-------------------|--------|
| `pipeline_runs` | `AddPipelineRunAsync` — only via dead `PipelineRunner.cs` (no callers) | `/api/pipeline-runs`, telemetry, Status/History pages | **DELETED** — no production writer; UI showed empty state forever |
| `pi_aggregations` | `AggregationService` — only via dead `PipelineRunner` | `/api/aggregations` (no frontend caller) | **DELETED** — dead end-to-end |
| `category_aggregations` | `AggregationService` — same dead wiring | `/api/aggregations` (no frontend caller) | **DELETED** — dead end-to-end |
| `scraper_pivots` | Nothing (no seeder; zero `IPivotEnricherService` implementations) | `PivotConfigurationService` (registered, never consumed) | **DELETED** — entire pivot subsystem removed as dead code |
| `source_fetch_histories` | `SourceFetchHistoryService` — `RecordFetchAsync`/`ShouldFetchAsync` have zero callers | Nothing | **DELETED** — was scaffolding for issue #213; rebuild when that feature is implemented |
| `scrape_events` | `ScrapeEventProgressReporter` (wired in `IngestionApp/Program.cs`) | `/api/telemetry`, `/api/scraper-progress`, `/api/data-source-state`, Status page | **KEPT** — alive; the #351 audit's "no production callers" claim was stale |
| `study_papers` | `PubMedScraperService` | Study detail, aggregations | **KEPT** — healthy |
| `rejected_investigator_names` | `Scrapers.Validation` CLI (manual) | `/api/rejected-names`, Status page | **KEPT** — in use; review/override workflow tracked in #344/#345 |

Related removals: `PipelineRunner.cs`, `AggregationService.cs`, `PivotConfigurationService.cs`, `PivotServiceRegistry.cs`, `IPivotEnricherService.cs`, `SourceFetchHistoryService.cs` + `ISourceFetchHistoryService.cs`, the `/api/pipeline-runs` and `/api/aggregations` endpoints, telemetry `pipelineRuns` field, and the frontend `PipelineHistory` page + "History" nav link.

> Note: `docs/GLOSSARY.md` still contains stale `pipeline_runs` entries — that file is human-maintained (AGENTS.md) and needs a human-authored PR to update.

---

## 9. Test Strategy

### 9.1 Three Database Testing Modes

| Mode | Class | Use Case |
|------|-------|----------|
| **Integration/IO** | `DbTestBase` | Testcontainers container per class, transaction rollback per method. Fast and isolated. |
| **Snapshot** | `SnapshotDb` | Container seeded with known golden data. Deterministic assertions. |
| **Persistent/fiddle** | `SnapshotDb(persist: true)` | Same as snapshot but no rollback — DB stays for manual inspection. |

### 9.2 Per-API Test Coverage

| Test Type | Class Pattern | Example |
|-----------|--------------|---------|
| **Unit** (fake HTTP with captured payloads) | `*Tests.cs` | `ClinicalTrialsGovClientTests`, `PubMedScraperServiceTests` |
| **Live API smoke** | `*IntegrationTests.cs` | `ClinicalTrialsGovIntegrationTests` |
| **Schema guard** (JSON shape validation) | `*SchemaGuardTests.cs` | `DataApiSchemaGuardTests` |

### 9.3 Data Loss Verification

Every scraper integration test must verify:
- Row counts in dependent tables match API data (e.g., if API returns 3 locations, assert `study_locations` has 3 rows for that study)
- No data is silently dropped during mapping
- Full field coverage is tested via assertions or schema guards

### 9.4 Test Utilities

Live in `Scrapers/Testing/`, shared via `InternalsVisibleTo`. Never duplicated.

---

## 10. PR Sequence

| PR | Scope | Description |
|----|-------|-------------|
| **1** | Study References | Create `StudyReferenceEntity` + migration. Store `References[]` from batch response. Eliminate per-study CT.gov HTTPS call in PubMedScraper. |
| **2** | Normalized Investigators | Create `InvestigatorPersonEntity`, `InvestigatorAffiliationEntity`, `StudyInvestigatorEntity`. Migration. Mapper updates. Retroactive deduplication. |
| **3** | Normalized PubMed Papers | Create `PubmedPaperEntity`, `InvestigatorPaperEntity`. Migration to migrate existing `PubmedStudyEntity` data. Update PubMedScraper. |
| **4** | Investigator Publication Scrub | New background service: search PubMed by name+affiliation, fetch all papers, link via junction. Event-driven from `investigator.discovered`. |
| **5** | Scalar Data Loss Fixes | `EligibilityCriteria`, `HealthyVolunteers`, `LeadSponsorName`, `CollaboratorNames`, `OrgStudyId`, `Masking` as scalar columns on `StudyEntity`. |
| **6** | Junction Data Loss Fixes | `StudyOutcomeEntity`, `StudyArmGroupEntity` as junction tables. Migrations. Mapper updates. |
