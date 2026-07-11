# Agent Guidelines

This repository has strict expectations for automated or human agents contributing changes. Read this document before making edits.

## Verification Policy
- **Never** merge or deploy changes that have not been verified locally. See `AI_CONTEXT`.
- Minimum verification for any PR: `dotnet build` and `dotnet test` (or equivalent commands for all affected projects).

## Testing Conventions
1. **Test Framework**: use MSTest only (`Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`, `MSTest.TestFramework`). Do not introduce xUnit/NUnit/etc.
2. **Repository/Service Fields**: Declare `StudyRepository` (and similar test dependencies) as `private` fields initialized in `[TestInitialize]`, not created inline in each test method. This is safe because MSTest creates a **new class instance per test method**, so each test gets its own field state — no concurrency concerns.
2. **Per-API Coverage**:
   - For every external API we call, add:
     - A unit test class that uses fake HTTP handlers with real captured payloads.
     - A live smoke test class that exercises the actual API endpoint (kept separate from unit tests).
   - Place future fixtures and handlers alongside existing ones under `Scrapers.Tests`.
3. **Fake Handlers & Real Data**:
   - Use deterministic fake `HttpMessageHandler` implementations that dequeue `HttpResponseMessage` instances created from *real* JSON responses captured from the API.
   - Store JSON fixtures in `Scrapers.Tests/Data/<ServiceName>/` and keep them unmodified except for truncating unrelated sections.
4. **Schema Change Alerts**:
   - Each API must include a schema guard test that fails when required fields disappear or change names. Implement this by inspecting the JSON fixtures (e.g., via `JsonDocument`).
5. **Live / Integration Tests**:
   - Keep them in separate files (e.g., `*IntegrationTests.cs`) for each API.
   - They must run as part of every `dotnet test` execution—do not gate them behind environment variables or `[Ignore]` attributes. Tagging with `[TestCategory("Integration")]` is fine for ad-hoc filtering.
   - Keep them lightweight (small payload requests) and add limited retry logic within the test to tolerate transient HTTP failures.
6. **Future APIs**:
   - When a new API is introduced, immediately add the corresponding unit tests, live tests, fixtures, and schema guard before merging.

## Database Persistence
- PostgreSQL is the source of truth for persisted studies.
- Connection string must be provided via `POSTGRES_CONNECTION_STRING` (both locally and in CI). The default expectation is a database named `clinical_trial_data` with sufficient privileges to create tables.
- The repository ensures the `studies` and `investigators` tables exist (`CREATE TABLE IF NOT EXISTS`). Do not add schema drift elsewhere—update the repository if schema changes are required.
- Schema changes are managed via Entity Framework Core migrations located under `Scrapers/Persistence/Migrations`. Always add/update migrations instead of writing SQL by hand.
- Database integration tests live under `Scrapers.IntegrationTests` and must connect to the configured PostgreSQL instance (see `POSTGRES_CONNECTION_STRING`). If the variable is unset, the tests automatically attempt `Host=localhost;Port=5432;Database=clinical_trial_data;Username=<current_user>`, and they also honor a local `Scrapers.IntegrationTests/.integrationtests.env` file when present. GitHub Actions uses the postgres service container defined in `.github/workflows/dotnet.yml` and automatically runs both unit and integration suites.
- Tests require a **fresh ingestion**: they truncate `studies`/`investigators` and insert the first five live studies before each integration test, so do not depend on manual DB state.

## GitHub Actions
- CI must run `dotnet build` and `dotnet test` on every push and pull request, covering unit, API integration, and PostgreSQL integration tests.

## Style Enforcement
- **`.editorconfig`** is the sole authority for all C# style rules (`IDE*` diagnostics). Never suppress `IDE*` in `.csproj` `<NoWarn>` — that creates a split-brain between Rider and `dotnet build`. Project-specific `<NoWarn>` is reserved for non-style warnings only (CA\*, NU\*, CS\*).

## Branching Workflow
- Always create new branches off `origin/main` (`git fetch origin main && git checkout origin/main -b feature/<name>`), not your local stale `main`. This ensures you start from the latest merged state.
- After branching, run `dotnet restore` and `dotnet build` once before making any changes to confirm the base compiles cleanly.

## Pull Request Expectations
- Summaries must mention how the change was tested.
- Include instructions if special setup was required.
- Ensure schema fixtures stay synchronized with real responses.
