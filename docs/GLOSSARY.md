# Clinical Trial Data Glossary

> ⛔ HUMAN-MAINTAINED — DO NOT EDIT WITH AI AGENTS
>
> This file defines domain vocabulary for this project. No automated tool, agent,
> or LLM may modify, append, or restructure this file. Changes require a
> human-authored PR with explicit review.

---

## Study Identifiers

### NCT ID
A unique identifier assigned by ClinicalTrials.gov to every registered clinical
trial. Format: `NCT` followed by 8 digits (e.g. `NCT02504502`). The primary key
for studies in this system.

### PMID
PubMed Identifier — a unique integer assigned by the National Library of
Medicine to every record in PubMed. Used to reference biomedical publications.
Distinct from a DOI.

### DOI
Digital Object Identifier — a persistent identifier for published documents
(e.g. `10.1000/xyz123`). Managed by registration agencies like CrossRef.
A paper may have both a PMID and a DOI.

---

## Study Concepts

### Clinical Trial
A research study that prospectively assigns human participants to one or more
health-related interventions to evaluate effects on health outcomes. Registered
on ClinicalTrials.gov with an NCT ID. In this system, everything ingested from
ClinicalTrials.gov is classified as a clinical trial.

### Paper / Publication
A document indexed in PubMed reporting research findings. A paper *may*
correspond to a clinical trial (e.g. reporting its results), but not all papers
are clinical trials — they may be reviews, meta-analyses, case reports,
editorials, etc.

### Phase
The stage of a clinical trial (I, II, III, IV). Phase I tests safety in a small
group; Phase II tests efficacy; Phase III compares against standard of care on a
large population; Phase IV monitors long-term effects post-approval.

### Overall Status
The current recruitment or completion state of a study on ClinicalTrials.gov.
Common values: `RECRUITING`, `ACTIVE_NOT_RECRUITING`, `COMPLETED`,
`TERMINATED`, `WITHDRAWN`, `SUSPENDED`, `NOT_YET_RECRUITING`, `ENROLLING_BY_INVITATION`.

### Condition
The disease, disorder, syndrome, or health condition being studied.
Stored in `study_conditions` linked to a study.

### Keyword
Searchable terms associated with a study on ClinicalTrials.gov. Stored in
`study_keywords`. Can contain noise (generic terms, study design labels) which
the ingestion pipeline filters.

### Enrollment
The target or actual number of participants in a clinical trial. Stored as
`EnrollmentCount` on `StudyEntity`.

---

## People

### PI (Principal Investigator)
The lead researcher responsible for conducting a clinical trial. Stored with
`RoleOnStudy = "PRINCIPAL_INVESTIGATOR"` on `StudyInvestigatorEntity`.

### Sub-Investigator
A researcher supporting the PI on a study. Distinguished from PI by
`RoleOnStudy` value.

### Investigator Person
A deduplicated person record in `investigator_persons` representing a real human
researcher. May be linked to multiple studies and multiple PubMed papers. Has
attributes like name, ORCID, NPI.

### Overall Official
A person listed in the ClinicalTrials.gov API in the `OverallOfficial` field.
May be the same as or different from the PI. Stored with
`IsOverallOfficial = true` on `StudyInvestigatorEntity`.

### Author
A contributor to a PubMed publication. Not necessarily an investigator in any
clinical trial. Authors are extracted from PubMed XML and used for ORCID
disambiguation.

---

## Investigator Data

### NPI
National Provider Identifier — a 10-digit unique identifier for healthcare
providers in the United States, issued by CMS. Used to join enrichment data
from Medicare and Open Payments.

### ORCID
Open Researcher and Contributor ID — a persistent digital identifier for
researchers. Used for disambiguation when an investigator's name matches
multiple NPI candidates.

### Affiliation
The institution or organization an investigator belongs to (e.g. "Mayo Clinic",
"Stanford University"). Stored in `investigator_affiliations`. Can be primary
(`IsPrimary = true`) or secondary.

### H-Index
A metric that measures both the productivity and citation impact of a
researcher. A researcher with h-index h has h papers that have each been cited
at least h times. Enriched from Semantic Scholar.

### Medicare Utilization
Data from CMS on the number of Medicare patients a healthcare provider treated,
services performed, and payments received. Stored in `medicare_utilizations`.

### Open Payments
Data from the CMS Sunshine Act showing financial relationships between
healthcare providers and pharmaceutical/medical device companies. Includes
research payments, general payments (consulting, travel, meals), and ownership
interests.

---

## Pipeline

### Ingestion
The process of fetching data from an external API (ClinicalTrials.gov, PubMed,
CMS, etc.) and persisting it to PostgreSQL.

### Scraper
A component or service responsible for fetching data from a specific external
source. Examples: `ClinicalTrialsIngestionService`, `PubMedScraperService`.

### Enrichment
The process of adding external data to an investigator record after initial
ingestion. Examples: NPI lookup, Medicare utilization, Open Payments, h-index.

### Event Queue
A pipeline event system (`pipeline_events` table) that drives asynchronous
processing. Events are enqueued by scraping services and consumed by enrichment
services. Each event has a type, status, retry count, and error tracking.

### Dead Letter Queue
Events that have permanently failed after exhausting their retry limit. Stored
in the `pipeline_events` table with status `failed`. Viewable in the Data
Quality page and `/api/event-queue/dead-letter` endpoint.

### Pipeline Run
One execution of the full ingestion pipeline, tracked in `pipeline_runs`. Stores
aggregate counts of studies, investigators, papers, and keywords processed.

---

## Data Quality

### Disambiguation
The process of determining whether two entity references refer to the same
real-world person or thing. Examples: matching an investigator name to an NPI
record, deduplicating investigator persons across studies.

### Rejected Entity
A keyword, investigator name, or affiliation that was filtered out during
ingestion because it was determined to be noise (generic term, role/occupation
label, pharma company name, etc.). Recorded in `rejected_entities`.

### Name Filter
The set of heuristic rules (and eventually ML classification) that determines
whether a name extracted from ClinicalTrials.gov represents a real human
investigator vs an organization, role label, or other non-person entity.

### Blocklist
A static list of terms rejected during ingestion. Being replaced by ML-based
classification in issue #162.

### Publication Type
A classification from PubMed's `PublicationTypeList` indicating what kind of
document a paper is. Values include: `Journal Article`, `Clinical Trial`,
`Review`, `Meta-Analysis`, `Randomized Controlled Trial`, `Case Reports`,
`Editorial`, `Letter`, `Comment`, etc. A single paper can have multiple types.
Stored as a comma-separated string in `pubmed_papers.publication_types`.

---

## Architecture

### DataApi
The REST API gateway (ASP.NET Core Minimal API, port 5003). The only component
that directly reads/writes to PostgreSQL. All other components (Frontend,
IngestionApp) communicate with the database through DataApi.

### Frontend
The Blazor Server web application (port 5001). Connects to DataApi via HTTP.
Uses interactive server-side rendering with SignalR.

### IngestionApp
A background service host that runs the scraping, enrichment, and event
processing pipelines. Connects to PostgreSQL via the shared Scrapers library.

### DbTestBase
Base class for integration tests that provides a Testcontainers-managed
PostgreSQL instance with transaction rollback per test method.

### SnapshotDb
Test database seeded with known golden data. Used for deterministic assertions
against a fixed dataset.

### Testcontainers
A .NET library that manages Docker containers for integration testing.
PostgreSQL instances are created and destroyed automatically per test class.

---

## Metrics

### Completion Rate
The percentage of a researcher's studies that reached completion:
`Completed / (Completed + Terminated)`. Excludes studies that are still in
progress (recruiting, active, enrolling, etc.).

### Enrollment Velocity
A measure of how quickly a researcher recruits participants into their studies.
Calculated from start dates and enrollment counts.

### Rejected Keyword Count
The number of keywords that were filtered out during a pipeline run. Tracked
in `data_source_state.rejected_keywords_total`.

### Study Paper Count
The number of PubMed papers linked to a study via `study_papers`. Distinct from
the total number of references in `study_references`.
