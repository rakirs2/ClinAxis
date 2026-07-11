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

### 2. Local Verification Required
- **Never** merge or deploy changes that have not been verified locally.
- Minimum verification for any PR: `dotnet build` (0 errors, 0 warnings) + `dotnet test` (all pass).
- Full verification: `docker compose up -d postgres && dotnet test` (tests run against Docker PostgreSQL).

### 3. Three DB Testing Modes (see `.opencode/plans/PLAN.md` for full design)
All database tests use `EphemeralPostgresDatabase` (Npgsql-based, no CLI `createdb`/`dropdb` calls). Single shared copy lives in `Scrapers/Testing/`.

| Mode | Class | Use Case |
|------|-------|----------|
| **Ephemeral** | `EphemeralDbTestBase` | Temp DB per test class, transaction rollback per method. Fast and isolated for integration tests. |
| **Snapshot** | `SnapshotDb` | Temp DB seeded with known golden data. Deterministic assertions against a fixed dataset. |
| **Persistent** | `SnapshotDb(persist: true)` | Same as snapshot but DB survives after tests. For manual inspection via any SQL tool. |

### 4. Tech Stack

| Layer | Technology | Why |
|-------|-----------|-----|
| Language | C# .NET 10 (`net10.0`) | Latest stable |
| Frontend | Blazor Server with `AddInteractiveServerComponents()` | SignalR built-in; Microsoft's standard |
| ORM | EF Core 10.x + Npgsql 9.x | Must match target framework |
| Database | PostgreSQL 15+ (via Docker for local dev) | Standard relational DB |
| Testing | MSTest only (`MSTest.TestAdapter` + `MSTest.TestFramework`) | No xUnit, no NUnit |
| CI/CD | GitHub Actions only | Source of truth for builds |
| Deploy | DigitalOcean Droplet — `dotnet publish` → SCP → systemd → Kestrel directly on port 80/443 | Per [Microsoft docs](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx) and [DigitalOcean docs](https://docs.digitalocean.com/developer-center/deploying-to-digitalocean-with-github-actions/) |

### 5. Test Conventions
- **Avoid mocks. Prefer pre-seeded data.** Most tests should use `SnapshotDb` with known golden data in a real PostgreSQL database. Only use fake HTTP handlers when testing an HTTP client against an external API that cannot be called in CI (e.g., third-party rate limits).
- **MSTest only.** Do not introduce xUnit, NUnit, or any other framework.
- **Per-API coverage:** Every external API we call must have:
  - A unit test class that uses fake HTTP handlers with real captured payloads (only when the live API cannot be hit every run).
  - A live smoke test class that exercises the actual API endpoint (kept separate from unit tests).
  - A schema guard test that inspects JSON response shape via `JsonDocument` and fails if required fields disappear.
- **Fixtures:** Store real captured JSON responses in `Scrapers.Tests/Data/<ServiceName>/`. Keep them unmodified except for truncating unrelated sections.
- **Live/integration tests:** Keep in separate `*IntegrationTests.cs` files. Tag with `[TestCategory("Integration")]` or `[TestCategory("HttpLive")]`. Do not gate behind `[Ignore]` or environment variables — they must run as part of `dotnet test`.
- **Test utilities live in `Scrapers/Testing/`** — shared via `InternalsVisibleTo`. Never duplicate.

### 6. Deployment Standard
- `dotnet publish --self-contained -r linux-x64`
- SCP publish output to Droplet
- systemd unit files for process management
- Kestrel serves HTTPS directly on port 80/443. TLS via .NET's built-in HTTPS + Let's Encrypt cert.
- No Docker for .NET apps in production. Docker is for local PostgreSQL only.
- No nginx. Keep the stack minimal.

### 7. It's OK to Delete Bad Code
- Refactor first, add features second.
- If code is duplicated, convoluted, or hard to test, delete it and replace with a simpler version.
- Do this in a dedicated PR before the feature PR.

### 8. Document Attempts — Do Not Repeat Failures
- Update `.opencode/plans/PLAN.md` with what was tried and what happened.
- Never retry an approach that already failed in a prior PR.
- Keep the decision log with choices and rationales.

### 9. When in Doubt, Default to Standard Docs
- .NET deploy to Linux: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx
- DO + GitHub Actions: https://docs.digitalocean.com/developer-center/deploying-to-digitalocean-with-github-actions/
- Default choices are better than custom ones. For this project, Kestrel serves directly (no nginx).

---

## Database Persistence
- PostgreSQL is the source of truth for persisted studies.
- Connection string via `POSTGRES_CONNECTION_STRING` env var. Default fallback: `Host=localhost;Port=5432;Database=clinical_trial_data;Username=<current_user>`.
- Schema changes managed via EF Core migrations in `Scrapers/Persistence/Migrations/`. Always add migrations (`dotnet ef migrations add`) instead of writing raw SQL.
- No schema drift outside the migrations system.
- **No production database exists yet.** All databases are local or ephemeral test databases. Schema migrations are low-risk — add them freely during development. Deployment to production will include running migrations at startup (current behavior via `EnsureSchemaAsync`).

## GitHub Actions
- CI must run `dotnet build` + `dotnet test` on every push and pull request, covering unit, DB integration, and live HTTP tests.
- Deploy workflow (added in a future PR) must run after CI passes, publishing to a DigitalOcean Droplet.

## Pull Request Expectations
- Summaries must mention how the change was tested.
- Include instructions if special setup was required.
- Ensure schema fixtures stay synchronized with real API responses.
- Keep PRs small — a reviewer should be able to understand the entire diff in under 5 minutes.
