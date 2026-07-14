-- Truncates ALL scraped data while keeping schema + configuration intact.
-- Run: psql -d clinical_trial_data -f scripts/truncate-local-db.sql
-- Or:  psql "$POSTGRES_CONNECTION_STRING" -f scripts/truncate-local-db.sql
--
-- Tables intentionally NOT truncated:
--   scraper_pivots        — scraper configuration (enabled, batch size, etc.)
--   __EFMigrationsHistory — EF Core migration tracking

TRUNCATE TABLE
    study_arm_groups,
    study_outcomes,
    study_references,
    study_investigators,
    study_papers,
    investigator_papers,
    investigator_affiliations,
    investigator_persons,
    pubmed_papers,
    study_keywords,
    study_conditions,
    study_locations,
    study_phases,
    studies,
    pipeline_events,
    pipeline_runs,
    scrape_events,
    pi_aggregations,
    category_aggregations,
    data_source_state,
    source_fetch_history,
    rejected_entities,
    rejected_investigator_names
RESTART IDENTITY CASCADE;
