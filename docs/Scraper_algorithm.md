# Scraper Algorithm

This is the documentation of the scrapers general algorithm for generating data.

Please read the scraper architecture for how jobs are processed.

1. Always scrape all of clinicaltrials.gov studies (and any other clinical trial study sources)
2. Store all relevant information.
3. For all human investigators from the clinical trials, scrape their publications (for now, just pub med). Start this step by first trying to crossreference their orcid and ncbi numbers. This is the best identifier.
4. For all of those investigators with a unique identifier, generate their studies.
5. From CMS.gov, try mapping and
6. We can add additional sources as necessary.

---

## Implementation Details

---

### 1. Full Scrape of ClinicalTrials.gov

**Source**: `Scrapers/ClinicalTrialsGov.cs` — `GetTrialRecordsBatchedAsync()`

#### 1.1 Pagination
- API: `https://clinicaltrials.gov/api/v2/studies?format=json&pageSize=100`
- Paginate via `nextPageToken` returned in each response
- Max 100 studies per page (`MaxPageSize`)

#### 1.2 Retry
- Exponential backoff: up to 3 attempts per page
- Retry on transient failures (network, 5xx)

#### 1.3 Deserialization
- JSON → `StudyListResponse` (model in `Models/ClinicalTrialsGov/StudyListResponse.cs`)
- Each study's `protocolSection` maps to `ClinicalTrialRecord` via `StudyPayloadExtensions.ToRecord()` (line 318)

#### 1.4 Entry Points
| Context | Class | Method |
|---------|-------|--------|
| Pipeline (integration test) | `ClinicalTrialsIngestionService` | `IngestAsync()` |
| Background service (production) | `ClinicalTrialsScrapeService` | `PerformScrapeAsync()` |

---

### 2. Persist All Fields (Zero Data Loss)

**Rule**: Every field returned by the API MUST be stored in PostgreSQL. See `docs/data_loss_remediation.md` for audit.

#### 2.1 Mapping
- `StudyRepository.UpdateStudiesWithClinicalTrialsAsync()` (`Persistence/StudyRepository.cs:46`)
- Maps `ClinicalTrialRecord` → `StudyEntity` via `MapRecordToEntity()`
- Also persists: Conditions, Keywords, Locations, References, Phases, Investigators

#### 2.2 Storage
- PostgreSQL via EF Core 10.x + Npgsql
- Connection: `POSTGRES_CONNECTION_STRING` env var (default: `Host=localhost;Port=5432;Database=clinical_trial_data;Username=<current_user>`)
- Schema managed via EF Core migrations in `Persistence/Migrations/`
- **No raw SQL** — all DB access through EF Core LINQ

#### 2.3 Deduplication
- Studies keyed by `NctId` — upsert (insert or update)
- Investigators deduplicated by name in `FindOrCreatePersonAsync()` (line 1189)

---

### 3. Investigator Classification (Human vs. Non-Human)

**Location**: `Scrapers/Utilities/NameFilter.cs` — `IsHumanName(name, role)`

The following checks run **in order**. First match determines the result.

#### 3.1 Hard Rejects (return false)
| # | Check | Example | Implementation |
|---|-------|---------|----------------|
| 1 | Null/empty name | `""` | `string.IsNullOrWhiteSpace` |
| 2 | Too short (< 3 chars) | `"AB"` | `trimmed.Length < 3` |
| 3 | Too long (> 100 chars) | 101+ char name | `trimmed.Length > 100` |
| 4 | Too many words (> 6) | `"A B C D E F G"` | `words.Length > 6` |
| 5 | Role prefix match | `"Contact for Public Queries"` | `RolePrefixes.Any(StartsWith)` — see §3.3 |
| 6 | Legal entity suffix | `"Research Lab Inc"`, `"Acme Ltd"` | Last word in `{INC, LTD, LLC, CORP, GMBH, AG, NV, PLC, SA, SARL, PTY, LIMITED, COMPANY, CO}` |
| 7 | Contains `" & "` | `"Johnson & Johnson"` | `trimmed.Contains(" & ")` |
| 8 | Organization keyword match | `"University of California"`, `"Global Clinical Registry"`, `"Study Director"` | `OrgKeywords.Any(ContainsWord)` — see §3.4 |
| 9 | Pharma blocklist (first word) | `"Pfizer CT.gov Call Center"`, `"GSK"`, `"Novartis"` | `PharmaBlocklist.Contains(words[0])` — see §3.5 |
| 10 | Single long word (> 20 chars) | `"Supercalifragilisticexpialidocious"` | `words.Length == 1 && trimmed.Length > 20` |
| 11 | All-caps 3+ words | `"UNIVERSITY OF CALIFORNIA"` | `words.Length >= 3 && words.All(w => w.All(c => !char.IsLower(c)))` |

#### 3.2 Known PI Role Override (return true)
If role is a known PI role (`KnownPiRoles`) AND all checks in §3.1 have passed, the name is accepted.

**Known PI roles**: `PRINCIPAL_INVESTIGATOR`, `SUB_INVESTIGATOR`, `STUDY_DIRECTOR`, `STUDY_CHAIR`, `STUDY_CO_CHAIR`, `STUDY_COORDINATOR`, `INVESTIGATOR`, `CO_INVESTIGATOR`

**Important**: This check runs AFTER organization keyword matching (step 8), so a role label in the name field (e.g., `"GSK Clinical Trials"` with role `STUDY_DIRECTOR`) is caught by "CLINICAL TRIALS" in OrgKeywords before the override fires.

#### 3.3 RolePrefixes (`RolePrefixes`)
Names starting with any of these are rejected (hard reject #5):
`CONTACT FOR`, `CONTACT`, `STUDY DIRECTOR`, `SCIENTIFIC CONTACT`, `PUBLIC QUERIES`, `PUBLIC CONTACT`, `SPONSOR`

#### 3.4 OrgKeywords (`OrgKeywords`)
Names containing any of these are rejected (hard reject #8). Uses substring match:
`UNIVERSITY`, `COLLEGE`, `INSTITUTE`, `HOSPITAL`, `CLINIC`, `LABORATORY`, `LABORATORIES`, `LAB`, `FOUNDATION`, `DEPARTMENT`, `COMMITTEE`, `ASSOCIATION`, `CORPORATION`, `COMPANY`, `PHARMA`, `PHARMACEUTICAL`, `BIOTECH`, `BIOSCIENCE`, `BIOSCIENCES`, `THERAPEUTICS`, `MEDICAL CENTER`, `CANCER CENTER`, `HEALTH SYSTEM`, `HEALTHCARE`, `NATIONAL INSTITUTE`, `SCHOOL OF`, `COLLEGE OF`, `OFFICE OF`, `CENTER FOR`, `CENTRE FOR`, `INSTITUTE OF`, `DIVISION OF`, `DEPARTMENT OF`, `BOARD OF`, `MINISTRY OF`, `FUND FOR`, `RESEARCH INSTITUTE`, `RESEARCH CENTER`, `RESEARCH CENTRE`, `CLINICAL RESEARCH`, `CLINICAL TRIAL`, `CLINICAL TRIALS`, `LIMITED LIABILITY`, `SOCIETE`, `GESELLSCHAFT`, `GMBH`, `AKTIENGESELLSCHAFT`, `AG`, `NV`, `PTY`, `PTY LTD`, `AND ASSOCIATES`, `AND COMPANY`, `& CO`, `& ASSOCIATES`, `DIRECTOR`, `MEDICAL`, `STUDY`, `CLINICAL`, `REGISTRY`, `MONITOR`, `COORDINATOR`, `MANAGEMENT`, `RESPONSIBLE`, `CALL CENTER`, `CENTER`, `CORPORATE`, `CARE`

#### 3.5 PharmaBlocklist (`PharmaBlocklist`)
If the **first word** of the name matches any entry, the name is rejected (hard reject #9). This catches multi-word names like `"Pfizer CT.gov Call Center"`:
`PFIZER`, `ROCHE`, `NOVARTIS`, `ASTRAZENECA`, `MERCK`, `SANOFI`, `BAYER`, `TAKEDA`, `AMGEN`, `GILEAD`, `BIOGEN`, `ABBVIE`, `REGENERON`, `MODERNA`, `BIONTECH`, `CELGENE`, `MYLAN`, `TEVA`, `SANDOZ`, `MEDTRONIC`, `STRYKER`, `BAUSCH`, `VIATRIS`, `BOEHRINGER`, `GSK`, `CHUGAI`, `UCB`

#### 3.6 Rejected Name Tracking
Filtered-out names are logged to `rejected_entities` table (type: `"investigator_name"`) for auditing.

#### 3.7 Study Completion Flag
- If **all** officials are rejected → study marked `IsIncomplete = true`
- If **at least one** official passes → study is `IsIncomplete = false`, human PIs are persisted
- Incomplete studies are stored but excluded from PubMed scraping

---

### 4. Investigator Deduplication

**Location**: `StudyRepository.FindOrCreatePersonAsync()` (line 1189)

#### 4.1 Name Parsing
- `NameParser.Parse()` extracts: honorific prefix (Dr., Prof.), first name, last name, suffix (MD, PhD)
- Used for dedup key: `fullName` = parsed name without prefix

#### 4.2 Dedup Key
1. Check batch-local cache (in-memory dictionary for current batch)
2. Check DB by `fullName` (parsed), then by `raw` (unparsed)
3. If not found → create new `InvestigatorPersonEntity`, enqueue `"investigator.discovered"` event

#### 4.3 Affiliation Storage
**Known gap**: `InvestigatorAffiliationEntity` is never populated from ClinicalTrials.gov data (only from future data sources).

---

### 5. Publication Scraping (PubMed)

**Entry Point**: Triggered by `"investigator.discovered"` event → `InvestigatorPublicationScrubService`

#### 5.1 Fetch PMIDs
- From `study_references` table for all studies linked to this investigator
- Query PubMed by investigator name + ORCID fallback

#### 5.2 Fetch Paper Details
- NCBI E-utilities (efetch) for each PMID
- Extract: title, authors, journal, year, DOI, ORCID from author list

#### 5.3 Link Papers
- Store in `investigator_papers` junction table
- If ORCID found in author list → update `InvestigatorPersonEntity.Orcid`

---

### 6. Unique Identifier Cross-Reference

**Goal**: Cross-reference ORCID and NCBI IDs for investigators to create a canonical identifier.

**Status**: NPI is assigned from NPPES NPI Registry during enrichment (see §6.2). ORCID is cross-referenced during publication scrub (§5). Full canonical ID resolution is pending (future PR).

---

### 6.5 Medicare Utilization Enrichment

**Entry Point**: Enqueued as `"medicare.utilization"` event by `InvestigatorEnrichmentService` after NPI is assigned.

**Source**: CMS Medicare Provider Utilization & Payment Data — Physician & Other Practitioners dataset
- API: `data.cms.gov/data-api/v1/dataset/{datasetUuid}/data`
- Dataset UUID: `8889d81e-2ee7-448f-8713-f071038289b5` (configurable via `CMS_MEDICARE_DATASET_UUID`)
- Auth: None (public data)
- Join Key: NPI (assigned during Investigator NPI enrichment, step 6.2)

**Pipeline**:

1. `InvestigatorEnrichmentService` enqueues `"medicare.utilization"` event when NPI is assigned to a person
2. `MedicareUtilizationService` (BackgroundService in IngestionApp) claims events every 30s
3. For each event:
   - Look up NPI in CMS Medicare API via `CmsMedicareClient.GetByNpiAsync()`
   - If found: store `MedicareUtilizationEntity` with 35+ fields (beneficiaries, payments, demographics, chronic conditions)
   - If not found: set `MedicareLookupResult = "not_found"` on person
   - If no NPI: set `MedicareLookupResult = "no_npi"`
4. Processed persons are skipped on subsequent cycles (guarded by `MedicareLookupAttemptedAt`)
5. Invalid event data (non-GUID, missing person) throws `InvalidOperationException` → event is failed properly (not silently skipped)

**Fallback**: `ProcessManualNpiPersonsAsync` runs when no queue events are pending — catches persons with NPI but no Medicare lookup yet (batch of 10 per cycle, ordered by creation date).

**Data Model**:

```
MedicareUtilizationEntity (table: medicare_utilizations)
├── Id (PK, serial)
├── InvestigatorPersonId (FK → investigator_persons.id)
├── DataYear (int)                         — configurable via MEDICARE_DATA_YEAR env var
├── ProviderType (string?)
├── TotalBeneficiaries (int?)
├── TotalServices (bigint?)
├── TotalSubmittedCharges (decimal?)
├── TotalMedicareAllowedAmount (decimal?)
├── TotalMedicarePaymentAmount (decimal?)
├── TotalMedicareStandardizedAmount (decimal?)
├── MedicareParticipationIndicator (string?)
├── BeneAgeLt65Count .. BeneMaleCount (int?) — beneficiary demographics (8 fields, age/sex/dual splits)
├── AvgRiskScore (decimal?)
├── MedicalServices, DrugServices (bigint?)
├── MedicalMedicarePayment, DrugMedicarePayment (decimal?)
├── ChronicConditionsJson (text) — serialized JSON of 22 condition prevalence percentages
└── CreatedAt, UpdatedAt (DateTime)
```

**Unique Index**: `(InvestigatorPersonId, DataYear)` — one row per person per year.

**Entities modified**:
- `MedicareUtilizationEntity` — new entity in `Scrapers/Persistence/Entities/`
- `InvestigatorPersonEntity.MedicareUtilizations` — navigation collection
- `InvestigatorPersonEntity.MedicareLookupAttemptedAt` — lookup guard (DateTime?)
- `InvestigatorPersonEntity.MedicareLookupResult` — result summary ("no_npi", "not_found", "found", "error")

**Client**: `CmsMedicareClient` (`Scrapers/Services/Enrichment/CmsMedicareClient.cs`)
- HTTP client wrapping the CMS data.gov API
- Methods: `GetByNpiAsync(npi)` — single record, `GetAllByNpiAsync(npi)` — all records
- Configurable dataset UUID (constructor parameter, defaults to current dataset)
- Non-2xx responses return null/empty (not found and API error are indistinguishable by design)

**Configuration** (env vars):
| Variable | Default | Purpose |
|----------|---------|---------|
| `CMS_MEDICARE_BASE_URL` | `https://data.cms.gov/data-api/v1/dataset/` | API base URL |
| `CMS_MEDICARE_DATASET_UUID` | `8889d81e-2ee7-448f-8713-f071038289b5` | Dataset identifier (changes per year) |
| `MEDICARE_DATA_YEAR` | current year (DateTime.UtcNow.Year) | Year to tag records with |

**Test Coverage**:
- Unit: `CmsMedicareClientTests` — 7 tests covering valid/invalid NPI, HTTP errors, custom dataset UUID, via FakeHttpMessageHandler + captured JSON fixture
- Integration: `MedicareUtilizationIntegrationTests` — 4 tests via DbTestBase: full field persistence, multi-year records, navigation property access, lookup field updates

**Exposed via DataApi**:
- `GET /api/investigators/{uuid}` — response includes `medicare` block with latest year's data
- Front-end `Investigator.razor` — Medicare Activity card showing 5 metric tiles (beneficiaries, services, allowed amount, payments, standardized amount) + collapsible chronic condition prevalence

**References**:
- `docs/data_loss_remediation.md` — all Medicare fields are persisted (verified)
- `IngestionApp/MedicareUtilizationService.cs` — background service implementation

---

### 7. CMS.gov Enrichment Ecosystem

**Current & planned data sources from CMS.gov, all joined by NPI.**

#### 7.1 Medicare Utilization (Phase 1 — IMPLEMENTED)
See §6.5 above. Physician-level Medicare utilization data (beneficiaries, payments, demographics, chronic conditions).

**Implementation**:
| Component | File |
|-----------|------|
| Entity | `Scrapers/Persistence/Entities/MedicareUtilizationEntity.cs` |
| Client | `Scrapers/Services/Enrichment/CmsMedicareClient.cs` |
| Service | `IngestionApp/MedicareUtilizationService.cs` |
| Migration | `Scrapers/Persistence/Migrations/20260715153621_AddMedicareUtilization.cs` |

#### 7.2 NPI/ORCID Enrichment (Phase 2 — PR #86, in review)
NPPES NPI Registry lookup and ORCID API cross-reference to assign persistent identifiers to investigators. See PR #86.

**Implementation**:
| Component | File |
|-----------|------|
| NPI Client | `Scrapers/Services/Cms/NppesNpiRegistryClient.cs` |
| ORCID Client | `Scrapers/Services/Cms/OrcidApiClient.cs` |
| Enrichment Pipeline | `Scrapers/Services/Cms/EnrichmentPipeline.cs` |

#### 7.3 CMS Open Payments (Phase 3 — Planned)
Research payments, general payments, ownership data from openpaymentsdata.cms.gov. See Issue #125.

#### 7.4 CMS Medicare Provider Listing (Phase 4 — Planned)
Provider demographic data via CSV import. See Issue #31 and PR #86.
