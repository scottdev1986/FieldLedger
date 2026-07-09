# FieldLedger

FieldLedger is a public multi-tenant farm operations SaaS scaffold built to demonstrate production-shaped SaaS architecture: organizations, roles, billing, deterministic demo data, C# API work, and Postgres RLS enforced behind an API layer.

The current implementation is Phase 0 from the design wiki: repository structure, runnable empty services, local Docker wiring, environment skeleton, and CI.

## Stack

| Area | Technology |
|---|---|
| Web | Next.js 16, React 19, Tailwind CSS 4 |
| API | ASP.NET Core on .NET 10 |
| Seeder | .NET 10 console app |
| Auth and database | External Supabase project |
| Billing | Stripe test mode |
| Local orchestration | Docker Compose |

## Repository Layout

```text
apps/
  web/      Next.js frontend scaffold
  api/      ASP.NET Core API scaffold
  seeder/   .NET console seeder scaffold
db/
  migrations/
  rls/
docs/
  fieldledger-design-wiki/
```

## Local Setup

```bash
cp .env.example .env
npm --prefix apps/web install
dotnet restore FieldLedger.slnx
```

Run services directly:

```bash
npm --prefix apps/web run dev
dotnet run --project apps/api
dotnet run --project apps/seeder -- --help
```

Or with Docker:

```bash
docker compose up --build
docker compose run --rm seeder --help
```

## Local URLs

| Service | URL |
|---|---|
| Web | http://localhost:3000 |
| API health | http://localhost:8080/health |
| API status | http://localhost:8080/api/status |

## Demo Logins

These accounts are for local/demo use only and will be created by a later seeder phase.

Owner: `owner@fieldledger.demo` / `FieldLedgerDemo!2026`  
Agronomist: `agronomist@fieldledger.demo` / `FieldLedgerDemo!2026`  
Viewer: `viewer@fieldledger.demo` / `FieldLedgerDemo!2026`
