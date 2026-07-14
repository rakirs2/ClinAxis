# Agent Guidelines

This repository has strict expectations for automated or human agents contributing changes. Read this document before making edits.

## Core Priorities (Read Before Any Work)

These are non-negotiable. Never violate these rules.

### 1. One Feature at a Time, Small PRs
- **Rule: 1 feature, 1 branch, 1 PR.** Every new feature gets its own branch from `origin/main`. No bundled changes.
- Each PR is a single logical change. No scope creep.
- Branch from `origin/main` — never from a stale local `main`.
- Before branching: `git fetch origin main && git checkout origin/main -b feature/<name>`
- After branching, run `dotnet restore && dotnet build` to confirm the base compiles cleanly.

### 2. Local Verification Required — Rider Runs All Tests
- **Rider runs every test locally.** All integration tests execute against a Docker PostgreSQL container. No tests are gated behind `[Ignore]`, environment checks, or manual approval.
- **Never** merge or deploy changes that have not passed locally.
- Minimum verification: `dotnet build` (0 errors, 0 warnings) + `dotnet test` (all pass).
- **Note on CA* rules**: Code analysis rules from `.editorconfig` are enforced locally but not in CI. Run `dotnet build -c Release` locally before pushing to validate them.
- Full verification: `dotnet test` (full test suite — Testcontainers manages Docker PostgreSQL automatically).
- **If local verification cannot be performed, the change must not proceed until the gap is resolved.** No exceptions.

### 3. Three DB Testing Modes (see `/docs/README.md` for full design)
All tests use a Testcontainers-managed PostgreSQL database (`clinical_trial_data_test`). No `CREATE DATABASE`/`DROP DATABASE` per test class. Isolation is via **transaction rollback** — each test writes inside a transaction, then rolls back. Single shared copy of the test base lives in `Scrapers/Testing/`.

| Mode | Class | Use Case |
|------|-------|----------|
| **Integration/IO** | `DbTestBase` | Testcontainers container per class, transaction rollback per method. Fast and isolated. |
| **Snapshot** | `SnapshotDb` | Container seeded with known golden data. Deterministic assertions against a fixed dataset. |
| **Persistent/fiddle** | `SnapshotDb(persist: true)` | Same as snapshot but no rollback — DB stays for manual inspection via any SQL tool. |

### 4. Single Gateway Rule: All DB Access Goes Through DataApi
- **Only DataApi talks to PostgreSQL.** Frontend, IngestionApp, and tests all access the database exclusively through DataApi's REST endpoints or via the shared `Scrapers` library's repositories.
- **No DbContext, Npgsql, or direct SQL in Frontend.** Frontend communicates with DataApi via HTTP (HttpClient). If something needs database access, it either calls DataApi or lives in the `Scrapers` library consumed by DataApi.
- **This is not negotiable.** It preserves a single contract boundary, enables independent scaling, and prevents tight coupling.

### 5. Tech Stack

| Layer | Technology | Why |
|-------|-----------|-----|
| Language | C# .NET 10 (`net10.0`) | Latest stable |
| Frontend | Blazor Server with `AddInteractiveServerComponents()` | SignalR built-in; Microsoft's standard |
| ORM | EF Core 10.x + Npgsql 9.x | Must match target framework |
| Database | PostgreSQL 15+ (via Docker for local dev) | Standard relational DB |
| Testing | MSTest only (`MSTest.TestAdapter` + `MSTest.TestFramework`) | No xUnit, no NUnit |
| CI/CD | GitHub Actions only | Source of truth for builds |
| Deploy | DigitalOcean Droplet — `dotnet publish` → SCP → systemd → Kestrel directly on port 80/443 | Per [Microsoft docs](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx) and [DigitalOcean docs](https://docs.digitalocean.com/developer-center/deploying-to-digitalocean-with-github-actions/) |

### 6. Zero Data Loss in Scraping — Persist All API Fields
- **Rule: Never discard API response data.** If an external API (ClinicalTrials.gov, PubMed, etc.) returns a field, it MUST be persisted to PostgreSQL.
- **Audit every scraper:**
  - When adding a new scraper or updating an existing one, verify that ALL fields from the API response have corresponding database storage.
  - If the API returns a field but no database table/column exists, create it (following the `StudyConditionEntity` / `StudyKeywordEntity` pattern for junction tables, or add scalar fields to the entity).
  - If a field is **intentionally ignored**, document WHY in a code comment with clear rationale.
- **Data loss discovered (current code):**
   - See `/docs/data_loss_remediation.md` for the full audit of lost fields and remediation roadmap.
   - `/docs/scraper_architecture.md` for the complete field coverage matrix.
- **Test coverage:** Every scraper integration test must verify:
  - Row counts in dependent tables match API data (e.g., if API returns 3 locations, assert `study_locations` has 3 rows for that study)
  - No data is silently dropped during mapping
  - Full field coverage is tested via assertions or schema guards

### 7. Tests Required for Every Code Change
- **Every code change MUST include corresponding tests.** A PR that adds or modifies production code without new or updated tests will be rejected.
- **New features** require tests covering the happy path, edge cases, and any data persistence verification.
- **Bug fixes** require a test that reproduces the bug before the fix and passes after.
- **Refactors** must not reduce existing test coverage. If existing tests don't cover the refactored code, add tests.
- **Patterns to follow:**
  - **Unit tests** (`Scrapers.Tests/`) — for client deserialization, mapping logic, and any code that can run without a database. Use `FakeHttpMessageHandler` with captured JSON fixtures for HTTP clients.
  - **DB integration tests** (`Scrapers.IntegrationTests/` via `DbTestBase`) — for repository persistence, data loss verification, and any code that writes to PostgreSQL.
  - **End-to-end tests** (in `Scrapers.IntegrationTests/`) — for full pipeline flows using `SnapshotDb` with golden data.
- **Data loss verification:** Any new entity or column must have a test that:
  1. Arranges known input data (fixture or inline)
  2. Runs it through the full mapping/persistence path
  3. Asserts row counts and field values in the corresponding database table(s)
- **Verify before committing:** Run `dotnet build` (0 errors, 0 warnings) + `dotnet test` (all pass) before creating the PR.

### 8. Test Conventions
- **Avoid mocks. Prefer pre-seeded data.** Most tests should use `SnapshotDb` with known golden data in a real PostgreSQL database. Only use fake HTTP handlers when testing an HTTP client against an external API that cannot be called in CI (e.g., third-party rate limits).
- **MSTest only.** Do not introduce xUnit, NUnit, or any other framework.
- **Per-API coverage:** Every external API we call must have:
  - A unit test class that uses fake HTTP handlers with real captured payloads (only when the live API cannot be hit every run).
  - A live smoke test class that exercises the actual API endpoint (kept separate from unit tests).
  - A schema guard test that inspects JSON response shape via `JsonDocument` and fails if required fields disappear.
- **Fixtures:** Store real captured JSON responses in `Scrapers.Tests/Data/<ServiceName>/`. Keep them unmodified except for truncating unrelated sections.
- **Live/integration tests:** Keep in separate `*IntegrationTests.cs` files. Tag with `[TestCategory("Integration")]` or `[TestCategory("HttpLive")]`. Do not gate behind `[Ignore]` or environment variables — they must run as part of `dotnet test`.
- **Test utilities live in `Scrapers/Testing/`** — shared via `InternalsVisibleTo`. Never duplicate.

### Running Tests from Rider
1. Ensure Docker Desktop is running (Testcontainers manages containers automatically — no manual `docker compose` needed)
2. Click **Run All Tests** in the test runner — every test, including integration tests against a real Postgres via Testcontainers, executes locally. No exceptions. No manual setup.

### 9. Deployment Standard
- `dotnet publish --self-contained -r linux-x64`
- SCP publish output to Droplet
- systemd unit files for process management
- Kestrel serves HTTPS directly on port 80/443. TLS via .NET's built-in HTTPS + Let's Encrypt cert.
- No Docker for .NET apps in production. Docker is for local PostgreSQL only.
- No nginx. Keep the stack minimal.

### 10. It's OK to Delete Bad Code
- Refactor first, add features second.
- If code is duplicated, convoluted, or hard to test, delete it and replace with a simpler version.
- Do this in a dedicated PR before the feature PR.

### 11. Document Attempts — Do Not Repeat Failures
- Update `.opencode/plans/PLAN.md` with what was tried and what happened.
- Never retry an approach that already failed in a prior PR.
- Keep the decision log with choices and rationales.

### 12. When in Doubt, Default to User Choice
- If there is no clear default documented here, **present options to the user and let them decide**. Don't guess and don't default to a personal preference.
- If standard docs answer the question, reference them (e.g., [MS Learn](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx), [DO guides](https://docs.digitalocean.com/developer-center/deploying-to-digitalocean-with-github-actions/)).
- If standard docs don't give a clear default, list the plausible approaches with trade-offs and ask.

### 13. YAML File Verification for GitHub Actions
- **Rule: All YAML file changes in `.github/workflows/` must pass local validation before creating a PR.**
- **Mandatory verifications before pushing:**
  1. `actionlint .github/workflows/<file.yml>` — zero errors permitted
  2. Line length check — all lines ≤ 120 characters
  3. Indentation consistency — 2-space indentation per `.editorconfig` [*.yml] rules
  4. No mixed tabs/spaces — spaces only
  5. Heredoc content alignment — consistent indentation with surrounding run block
  6. sudo permissions — workflows writing to `/etc/` must use `sudo tee` or `sudo -c` to avoid "Permission denied" errors
- **GitHub Actions YAML indentation standard:**
  - Top-level job list items: 2 spaces + `-`
  - Job properties (needs, if, runs-on): 4 spaces
  - Step list items within a job: 6 spaces + `-`
  - Step properties (name, run, uses, with): 8 spaces
  - `run: |` script content: 10 spaces base + 1 space per nesting level
  - Heredoc delimiters (EOF, EOSSH): align with run block content indentation
- **Line length standard:** Maximum 120 characters per line (practical GitHub Actions limit)
- **Actionlint installation:** `brew install actionlint` (Homebrew)
- **Validation script:** Run `bash scripts/validate-workflows.sh` before creating a PR — automatically checks:
  - actionlint compliance
  - Line length violations
  - sudo permission issues (detects unsafe `/etc/` writes)
  - Indentation consistency
- **Common mistakes to avoid:**
  - Adding extra spaces when editing step blocks → breaks YAML list syntax (actionlinter catches this)
  - Indenting heredoc content that shouldn't be indented → adds unwanted spaces to file output
  - Long command chains in `run:` blocks → break into multiple lines using shell variables
  - Not aligning nested heredoc delimiters → breaks shell parsing
  - Writing to `/etc/` without sudo → causes "Permission denied" errors at deploy time
- **PR verification checklist:**
  - [ ] Run validation script: `bash scripts/validate-workflows.sh` with zero failures
  - [ ] All workflows pass actionlint with zero errors
  - [ ] No lines exceed 120 characters in `.github/workflows/*.yml`
  - [ ] Sudo permission checks pass (no unsafe `/etc/` writes)
  - [ ] Include validation results in PR description: ✅ `bash scripts/validate-workflows.sh` passes
- **If local verification cannot be performed, the change must not proceed.** No exceptions.

---

## Database Persistence
- PostgreSQL is the source of truth for persisted studies.
- Connection string via `POSTGRES_CONNECTION_STRING` env var. Default fallback: `Host=localhost;Port=5432;Database=clinical_trial_data;Username=<current_user>`.
- Schema changes managed via EF Core migrations in `Scrapers/Persistence/Migrations/`. Always add migrations (`dotnet ef migrations add`) instead of writing raw SQL.
- No schema drift outside the migrations system.
- **No raw SQL strings anywhere in application code.** All database operations use EF Core LINQ queries. No `FromSqlRaw`, `ExecuteSqlRaw`, `SqlQuery`, or direct `NpgsqlCommand` calls. The sole exception is EF Core migration SQL (auto-generated by `dotnet ef migrations add`).
- **No production database exists yet.** All databases are local or ephemeral test databases. Schema migrations are low-risk — add them freely during development. Deployment to production will include running migrations at startup (current behavior via `EnsureSchemaAsync`).

## GitHub Actions
- CI must run `dotnet build` + `dotnet test` on every push and pull request, covering unit, DB integration, and live HTTP tests.
- **No push to `main` without all integration tests passing.** The CI workflow blocks the merge if any test — unit, integration, or live HTTP — fails.
- Deploy workflow (added in a future PR) runs only after CI passes, publishing to a DigitalOcean Droplet.

## Style Enforcement
- **`.editorconfig`** is the sole authority for ALL code analysis, including both style (`IDE*`) and quality (`CA*`) rules. No `#pragma warning disable` anywhere in the codebase. No `<NoWarn>` in any `.csproj` file. If a rule fires, fix the code — do not suppress it.
- When a public API surface is needed for testing (e.g., `partial class Program` for `WebApplicationFactory`), use `<InternalsVisibleTo>` in `.csproj` instead of making types `public` or suppressing CA1515.

## Branching Workflow
- Always create new branches off `origin/main` (`git fetch origin main && git checkout origin/main -b feature/<name>`), not your local stale `main`. This ensures you start from the latest merged state.
- After branching, run `dotnet restore` and `dotnet build` once before making any changes to confirm the base compiles cleanly.

## Pull Request Expectations
- Summaries must mention how the change was tested.
- Include instructions if special setup was required.
- Ensure schema fixtures stay synchronized with real API responses.
- Keep PRs small — a reviewer should be able to understand the entire diff in under 5 minutes.
