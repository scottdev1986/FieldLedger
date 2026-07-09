# FieldLedger

[![CI](https://github.com/scottdev1986/FieldLedger/actions/workflows/ci.yml/badge.svg)](https://github.com/scottdev1986/FieldLedger/actions/workflows/ci.yml)

FieldLedger is a public multi-tenant farm operations SaaS built to demonstrate production-style SaaS architecture: organizations, roles, plan gating, deterministic demo data, C# API work, and Postgres RLS enforced behind an API layer. The whole stack is self-contained — no external accounts, API keys, or hosted services are required to run it.

> I built agricultural operations reporting professionally; that code is gated, so I built this public version for you to inspect.

## Two-minute demo

```bash
cp .env.example .env   # optional; every default works locally
docker compose up --build
docker compose run --rm seeder
```

Then open http://localhost:3000 and sign in with a demo account:

| Role | Email | Password |
|---|---|---|
| Owner | `owner@fieldledger.demo` | `FieldLedgerDemo!2026` |
| Agronomist | `agronomist@fieldledger.demo` | `FieldLedgerDemo!2026` |
| Viewer | `viewer@fieldledger.demo` | `FieldLedgerDemo!2026` |

Suggested walkthrough: sign in as the owner → dashboard, field detail, and insights over three seeded seasons → Members roles → Plan & billing → Upgrade to Pro (instant, audited, no payment) → generate the season report and export CSV → sign in as the viewer and note every edit control is gone — and that the API and database refuse the writes anyway.

## What this proves

- Multi-tenant SaaS MVP architecture (orgs, members, roles, fields, seasons, activities).
- First-party email/password auth with API-issued JWTs (ASP.NET Core, PBKDF2 hashing).
- Postgres RLS defense-in-depth behind a C# API — the API's database role cannot bypass RLS.
- In-app Free/Pro plan gating with a documented payment-provider seam: entitlements change only through an owner-only, audited `security definer` path (`app.set_org_plan`). A real deployment would slot a payment provider's webhook handler behind that same seam.
- Server-side enforcement everywhere: Free field limit (API check + database trigger), Pro-gated CSV export and season report.
- Deterministic C# demo-data seeding (same inputs → identical demo data) and a C# SQL migration runner.

## Stack

| Area | Technology |
|---|---|
| Web | Next.js 16, React 19, Tailwind CSS 4, TanStack Query, Recharts |
| API | ASP.NET Core on .NET 10, Npgsql (raw SQL) |
| Database | PostgreSQL 17 (Docker container), forced RLS |
| Auth | First-party: users table + PBKDF2 hashes, HS256 JWTs issued by the API |
| Plans | Entitlements table, in-app owner-only upgrade/downgrade, `plan_changes` audit |
| Seeder / migrations | .NET 10 console app (`seed` and `migrate` modes) |
| Orchestration | Docker Compose: `db`, `migrate`, `api`, `web`, `seeder` |

## Architecture

```text
Browser (Next.js)
  -> POST /api/auth/login            first-party credentials
  <- API-issued JWT (HS256)
  -> Bearer JWT on every API call
       ASP.NET Core API
         validates its own JWT
         opens a transaction per request:
           set local role authenticated;
           select set_config('app.user_id', <sub>, true);
         runs application SQL
       PostgreSQL 17
         RLS policies check membership + role for every row
         field-limit trigger backs up the API's plan checks
```

The design is maintained as a wiki: [docs/fieldledger-design-wiki](docs/fieldledger-design-wiki/index.md) — architecture, domain model, schema, auth/RLS, plans, testing, ADRs (see [ADR 0006](docs/fieldledger-design-wiki/adr/0006-self-contained-postgres-first-party-auth.md) and [ADR 0007](docs/fieldledger-design-wiki/adr/0007-in-app-plan-management-no-external-billing.md) for why there are no external services).

## RLS walkthrough

The API issues and validates its own JWTs, then forwards the verified user id into Postgres per request as `app.user_id` inside a transaction. RLS policies use that id to check organization membership and role. Tenant isolation is enforced in the database itself: even a buggy API handler cannot return cross-org rows, because `fieldledger_api` has no `BYPASSRLS`.

Prove it from psql with the simulation script ([templates/rls-demo.sql](docs/fieldledger-design-wiki/templates/rls-demo.sql)): set `app.user_id` to a demo user, select from another org's `fields`, and get zero rows.

The full assertion suite (tenant isolation, viewer write denial, owner-only plan changes, entitlement write protection, field-limit trigger) runs locally and in CI:

```bash
docker compose exec -T db psql -U postgres -d fieldledger -v ON_ERROR_STOP=1 -f - < db/rls/rls-checks.sql
```

## Tests

```bash
dotnet test FieldLedger.slnx        # unit tests; set TEST_DATABASE_URL for live-DB integration tests
npm --prefix apps/web run typecheck && npm --prefix apps/web run lint && npm --prefix apps/web run build
```

CI runs web typecheck/lint/build, the .NET suite (including live-database integration tests against a Postgres 17 service container), migrations, and the RLS assertion suite on every push.

## Repository layout

```text
apps/
  web/        Next.js frontend
  api/        ASP.NET Core API
  api.Tests/  xunit unit + live-database integration tests
  seeder/     .NET console app: `migrate` and `seed`
db/
  init/       role bootstrap (docker-entrypoint-initdb.d)
  migrations/ ordered SQL migrations (schema, functions, RLS)
  rls/        RLS assertion suite
docs/
  fieldledger-design-wiki/   maintained design wiki + ADRs
  api-contract.md            frontend/backend JSON contract
  frontend-design-brief.md   UI design system brief
```

## Security notes

- Dev credentials in `.env.example` and demo passwords are local-demo-only by design.
- `users.password_hash` is not selectable by the app's database role; login/register go through dedicated `security definer` functions.
- Browser JWT storage in `localStorage` is an accepted portfolio-v1 tradeoff, documented in the wiki with the httpOnly-cookie BFF alternative.
- No payment is processed anywhere; the UI says so explicitly.
