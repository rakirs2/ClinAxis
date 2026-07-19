# Scraper Algorithm

This document separates the scraper algorithm into two sections:

- **⛔ Human-Maintained** — High-level algorithm design decisions (heuristic rules, strategy, policy). An AI agent may propose changes but must never apply them without human review.
- **✅ AI-Maintained** — Implementation details and code references. An AI agent may freely update these as the codebase evolves, following established patterns.

> Please read [scraper_architecture.md](scraper_architecture.md) for the overall architecture and entity model.

---

## ⛔ Human-Maintained — High-Level Algorithm Design

*An AI agent may propose changes to this section but must never apply them without human review.*

### 1. Pipeline Order

1. Always scrape all of ClinicalTrials.gov studies (and any other clinical trial study sources)
2. Store all relevant information.
3. For all human investigators from the clinical trials, scrape their publications (for now, just PubMed). Start this step by first trying to cross-reference their ORCID and NCBI numbers. This is the best identifier.
4. For all of those investigators with a unique identifier, generate their studies.
5. From CMS.gov, try mapping and enrich.
6. We can add additional sources as necessary.

### 2. Pagination Strategy

- API: `https://clinicaltrials.gov/api/v2/studies?format=json&pageSize=100`
- Paginate via `nextPageToken` returned in each response
- Max 100 studies per page (`MaxPageSize`)
- **Why 100**: API maximum. Larger pages reduce HTTP round-trips while staying within reasonable response size.

### 3. Retry/Backoff Policy

- Exponential backoff: up to 3 attempts per page
- Retry on transient failures (network, 5xx)
- **Why 3**: Balances reliability against wall-clock time. First retry recovers most transient failures; second catches intermittent issues; third is the safety net.
- **Why exponential backoff**: Avoids hammering the API during an outage.

### 4. Zero Data Loss Principle

**Rule**: Every field returned by the API MUST be stored in PostgreSQL. See `docs/data_loss_remediation.md` for audit.

No field is intentionally ignored. If a field cannot be stored, document the reason in a code comment.

### 5. Investigator Classification (Human vs. Non-Human)

**Location**: `Scrapers/Utilities/NameFilter.cs` — `IsHumanName(name, role)`

The following checks run **in order**. First match determines the result.

#### 5.1 Hard Rejects (return false)

| # | Check | Example | Implementation |
|---|-------|---------|----------------|
| 1 | Null/empty name | `""` | `string.IsNullOrWhiteSpace` |
| 2 | Too short (< 3 chars) | `"AB"` | `trimmed.Length < 3` |
| 3 | Too long (> 100 chars) | 101+ char name | `trimmed.Length > 100` |
| 4 | Too many words (> 6) | `"A B C D E F G"` | `words.Length > 6` |
| 5 | Role prefix match | `"Contact for Public Queries"` | `RolePrefixes.Any(StartsWith)` — see §5.3 |
| 6 | Legal entity suffix | `"Research Lab Inc"`, `"Acme Ltd"` | Last word in `{INC, LTD, LLC, CORP, GMBH, AG, NV, PLC, SA, SARL, PTY, LIMITED, COMPANY, CO}` |
| 7 | Contains `" & "` | `"Johnson & Johnson"` | `trimmed.Contains(" & ")` |
| 8 | Organization keyword match | `"University of California"`, `"Global Clinical Registry"`, `"Study Director"` | `OrgKeywords.Any(ContainsWord)` — see §5.4 |
| 9 | Pharma blocklist (first word) | `"Pfizer CT.gov Call Center"`, `"GSK"`, `"Novartis"` | `PharmaBlocklist.Contains(words[0])` — see §5.5 |
| 10 | Single long word (> 20 chars) | `"Supercalifragilisticexpialidocious"` | `words.Length == 1 && trimmed.Length > 20` |
| 11 | All-caps 3+ words | `"UNIVERSITY OF CALIFORNIA"` | `words.Length >= 3 && words.All(w => w.All(c => !char.IsLower(c)))` |

#### 5.2 Known PI Role Override (return true)

If role is a known PI role (`KnownPiRoles`) AND all checks in §5.1 have passed, the name is accepted.

**Known PI roles**: `PRINCIPAL_INVESTIGATOR`, `SUB_INVESTIGATOR`, `STUDY_DIRECTOR`, `STUDY_CHAIR`, `STUDY_CO_CHAIR`, `STUDY_COORDINATOR`, `INVESTIGATOR`, `CO_INVESTIGATOR`

**Important**: This check runs AFTER organization keyword matching (step 8), so a role label in the name field (e.g., `"GSK Clinical Trials"` with role `STUDY_DIRECTOR`) is caught by `"CLINICAL TRIALS"` in OrgKeywords before the override fires.

#### 5.3 RolePrefixes

Names starting with any of these are rejected (hard reject #5):
`CONTACT FOR`, `CONTACT`, `STUDY DIRECTOR`, `SCIENTIFIC CONTACT`, `PUBLIC QUERIES`, `PUBLIC CONTACT`, `SPONSOR`

#### 5.4 OrgKeywords

Names containing any of these are rejected (hard reject #8). Uses substring match:
`UNIVERSITY`, `COLLEGE`, `INSTITUTE`, `HOSPITAL`, `CLINIC`, `LABORATORY`, `LABORATORIES`, `LAB`, `FOUNDATION`, `DEPARTMENT`, `COMMITTEE`, `ASSOCIATION`, `CORPORATION`, `COMPANY`, `PHARMA`, `PHARMACEUTICAL`, `BIOTECH`, `BIOSCIENCE`, `BIOSCIENCES`, `THERAPEUTICS`, `MEDICAL CENTER`, `CANCER CENTER`, `HEALTH SYSTEM`, `HEALTHCARE`, `NATIONAL INSTITUTE`, `SCHOOL OF`, `COLLEGE OF`, `OFFICE OF`, `CENTER FOR`, `CENTRE FOR`, `INSTITUTE OF`, `DIVISION OF`, `DEPARTMENT OF`, `BOARD OF`, `MINISTRY OF`, `FUND FOR`, `RESEARCH INSTITUTE`, `RESEARCH CENTER`, `RESEARCH CENTRE`, `CLINICAL RESEARCH`, `CLINICAL TRIAL`, `CLINICAL TRIALS`, `LIMITED LIABILITY`, `SOCIETE`, `GESELLSCHAFT`, `GMBH`, `AKTIENGESELLSCHAFT`, `AG`, `NV`, `PTY`, `PTY LTD`, `AND ASSOCIATES`, `AND COMPANY`, `& CO`, `& ASSOCIATES`, `DIRECTOR`, `MEDICAL`, `STUDY`, `CLINICAL`, `REGISTRY`, `MONITOR`, `COORDINATOR`, `MANAGEMENT`, `RESPONSIBLE`, `CALL CENTER`, `CENTER`, `CORPORATE`, `CARE`

#### 5.5 PharmaBlocklist

If the **first word** of the name matches any entry, the name is rejected (hard reject #9). This catches multi-word names like `"Pfizer CT.gov Call Center"`:
`PFIZER`, `ROCHE`, `NOVARTIS`, `ASTRAZENECA`, `MERCK`, `SANOFI`, `BAYER`, `TAKEDA`, `AMGEN`, `GILEAD`, `BIOGEN`, `ABBVIE`, `REGENERON`, `MODERNA`, `BIONTECH`, `CELGENE`, `MYLAN`, `TEVA`, `SANDOZ`, `MEDTRONIC`, `STRYKER`, `BAUSCH`, `VIATRIS`, `BOEHRINGER`, `GSK`, `CHUGAI`, `UCB`

#### 5.6 Study Completion Flag

- If **all** officials are rejected → study marked `IsIncomplete = true`
- If **at least one** official passes → study is `IsIncomplete = false`, human PIs are persisted
- Incomplete studies are stored but excluded from PubMed scraping

### 6. Investigator Deduplication Strategy

#### 6.1 Name Parsing
- `NameParser.Parse()` extracts: honorific prefix (Dr., Prof.), first name, last name, suffix (MD, PhD)
- Used for dedup key: `fullName` = parsed name without prefix

#### 6.2 Dedup Key
1. Check batch-local cache (in-memory dictionary for current batch)
2. Check DB by `fullName` (parsed), then by `raw` (unparsed)
3. If not found → create new `InvestigatorPersonEntity`, enqueue `"investigator.discovered"` event

### 7. Event Queue Lifecycle

- **Event types**: `studies.discovered`, `investigator.enrichment`, `investigator.discovered`, `investigator.publications.scrubbed`
- **Lifecycle**: Enqueue → Pending → Claimed → Completed
  - Failure → Retry (up to 3, exponential backoff) → Dead Letter
- **Why 3 retries**: Same rationale as CT.gov retry policy.
- **Why 30-minute claim timeout**: Allows processing even during slow enrichment operations.
- **Dead letter**: Manual retry or ignore via DataApi endpoints.

### 8. Phase Normalization

Maps ClinicalTrials.gov phase strings to canonical values.
**Constants**: `EarlyPhase1`, `Phase1`, `Phase2`, `Phase3`, `Phase4`, `NotApplicable`

---

## ✅ AI-Maintained — Implementation Details

*An AI agent may freely update this section as code evolves, following established patterns. This includes:*
- *Adding new fields to existing entities (following entity + junction table pattern)*
- *Adding new HTTP client methods for existing endpoints*
- *Adding new test fixtures and test cases*
- *Refactoring internal helper methods*
- *Updating inline code comments*
- *Adding new migrations*
- *Adding constants in the established style*

### 9. Full Scrape Implementation

**Source**: `Scrapers/ClinicalTrialsGov.cs` — `GetTrialRecordsBatchedAsync()`

#### 9.1 Entry Points
| Context | Class | Method |
|---------|-------|--------|
| Pipeline (integration test) | `ClinicalTrialsIngestionService` | `IngestAsync()` |
| Background service (production) | `ClinicalTrialsScrapeService` | `PerformScrapeAsync()` |

#### 9.2 Deserialization
- JSON → `StudyListResponse` (model in `Models/ClinicalTrialsGov/StudyListResponse.cs`)
- Each study's `protocolSection` maps to `ClinicalTrialRecord` via `StudyPayloadExtensions.ToRecord()` (line 318)

### 10. Persistence Implementation

**Source**: `StudyRepository.UpdateStudiesWithClinicalTrialsAsync()` (`Persistence/StudyRepository.cs:46`)

#### 10.1 Mapping
- Maps `ClinicalTrialRecord` → `StudyEntity` via `MapRecordToEntity()`
- Also persists: Conditions, Keywords, Locations, References, Phases, Investigators

#### 10.2 Storage
- PostgreSQL via EF Core 10.x + Npgsql
- Connection: `POSTGRES_CONNECTION_STRING` env var (default: `Host=localhost;Port=5432;Database=clinical_trial_data;Username=<current_user>`)
- Schema managed via EF Core migrations in `Persistence/Migrations/`
- **No raw SQL** — all DB access through EF Core LINQ

#### 10.3 Rejected Name Tracking
Filtered-out names are logged to `rejected_entities` table (type: `"investigator_name"`) for auditing.

### 11. Publication Scraping (PubMed)

**Entry Point**: Triggered by `"investigator.discovered"` event → `InvestigatorPublicationScrubService`

#### 11.1 Fetch PMIDs
- From `study_references` table for all studies linked to this investigator
- Query PubMed by investigator name + ORCID fallback

#### 11.2 Fetch Paper Details
- NCBI E-utilities (efetch) for each PMID
- Extract: title, authors, journal, year, DOI, ORCID from author list

#### 11.3 Link Papers
- Store in `investigator_papers` junction table
- If ORCID found in author list → update `InvestigatorPersonEntity.Orcid`

### 12. Unique Identifier Cross-Reference

**Goal**: Cross-reference ORCID and NCBI IDs for investigators to create a canonical identifier.

**Status**: NPI is assigned from NPPES NPI Registry during enrichment. ORCID is cross-referenced during publication scrub. Full canonical ID resolution is pending (future PR).

### 13. Medicare Utilization Enrichment

**Entry Point**: Enqueued as `"medicare.utilization"` event by `InvestigatorEnrichmentService` after NPI is assigned.

**Source**: CMS Medicare Provider Utilization & Payment Data — Physician & Other Practitioners dataset
- API: `data.cms.gov/data-api/v1/dataset/{datasetUuid}/data`
- Dataset UUID: `8889d81e-2ee7-448f-8713-f071038289b5` (configurable via `CMS_MEDICARE_DATASET_UUID`)
- Auth: None (public data)
- Join Key: NPI (assigned during Investigator NPI enrichment)

**Pipeline**:
1. `InvestigatorEnrichmentService` enqueues `"medicare.utilization"` event when NPI is assigned to a person
2. `MedicareUtilizationService` (BackgroundService in IngestionApp) claims events every 30s
3. For each event:
   - Look up NPI in CMS Medicare API via `CmsMedicareClient.GetByNpiAsync()`
   - If found: store `MedicareUtilizationEntity` with 35+ fields (beneficiaries, payments, demographics, chronic conditions)
   - If not found: set `MedicareLookupResult = "not_found"` on person
   - If no NPI: set `MedicareLookupResult = "no_npi"`
4. Processed persons are skipped on subsequent cycles (guarded by `MedicareLookupAttemptedAt`)
5. Invalid event data (non-GUID, missing person) throws `InvalidOperationException` → event is failed properly

**Fallback**: `ProcessManualNpiPersonsAsync` runs when no queue events are pending — catches persons with NPI but no Medicare lookup yet (batch of 10 per cycle, ordered by creation date).

**Configuration** (env vars):
| Variable | Default | Purpose |
|----------|---------|---------|
| `CMS_MEDICARE_BASE_URL` | `https://data.cms.gov/data-api/v1/dataset/` | API base URL |
| `CMS_MEDICARE_DATASET_UUID` | `8889d81e-2ee7-448f-8713-f071038289b5` | Dataset identifier (changes per year) |
| `MEDICARE_DATA_YEAR` | current year (`DateTime.UtcNow.Year`) | Year to tag records with |

**Test Coverage**:
- Unit: `CmsMedicareClientTests` — 7 tests covering valid/invalid NPI, HTTP errors, custom dataset UUID, via FakeHttpMessageHandler + captured JSON fixture
- Integration: `MedicareUtilizationIntegrationTests` — 4 tests via DbTestBase: full field persistence, multi-year records, navigation property access, lookup field updates

**Exposed via DataApi**:
- `GET /api/investigators/{uuid}` — response includes `medicare` block with latest year's data
- Front-end `Investigator.razor` — Medicare Activity card showing 5 metric tiles (beneficiaries, services, allowed amount, payments, standardized amount) + collapsible chronic condition prevalence

### 14. CMS.gov Enrichment Ecosystem

**Current & planned data sources from CMS.gov, all joined by NPI.**

#### 14.1 Medicare Utilization (Phase 1 — IMPLEMENTED)
See §13 above. Physician-level Medicare utilization data.

**Implementation**:
| Component | File |
|-----------|------|
| Entity | `Scrapers/Persistence/Entities/MedicareUtilizationEntity.cs` |
| Client | `Scrapers/Services/Enrichment/CmsMedicareClient.cs` |
| Service | `IngestionApp/MedicareUtilizationService.cs` |
| Migration | `Scrapers/Persistence/Migrations/20260715153621_AddMedicareUtilization.cs` |

#### 14.2 NPI/ORCID Enrichment (Phase 2)
NPPES NPI Registry lookup and ORCID API cross-reference to assign persistent identifiers to investigators. See PR #86.

**Implementation**:
| Component | File |
|-----------|------|
| NPI Client | `Scrapers/Services/Cms/NppesNpiRegistryClient.cs` |
| ORCID Client | `Scrapers/Services/Cms/OrcidApiClient.cs` |
| Enrichment Pipeline | `Scrapers/Services/Cms/EnrichmentPipeline.cs` |

#### 14.3 CMS Open Payments (Phase 3 — Planned)
Research payments, general payments, ownership data from openpaymentsdata.cms.gov. See Issue #125.

#### 14.4 CMS Medicare Provider Listing (Phase 4 — Planned)
Provider demographic data via CSV import. See Issue #31 and PR #86.
