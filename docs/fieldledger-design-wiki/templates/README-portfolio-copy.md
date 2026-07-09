# FieldLedger

FieldLedger is a public multi-tenant farm operations SaaS built to demonstrate production-style SaaS architecture: organizations, roles, plan gating, deterministic demo data, C# API work, and Postgres RLS enforced behind an API layer. The whole stack is self-contained — no external accounts, API keys, or hosted services are required to run it.

> I built agricultural operations reporting professionally; that code is gated, so I built this public version for you to inspect.

## What this proves

- Multi-tenant SaaS MVP architecture.
- Organization/member/role modeling.
- First-party email/password auth with API-issued JWTs (ASP.NET Core).
- Postgres RLS defense-in-depth behind a C# API.
- In-app Free/Pro plan gating with a documented payment-provider seam — a real deployment would slot a payment provider's webhook handler behind the same server-side entitlement path.
- Server-side Free/Pro enforcement (field limits, gated exports and reports).
- Deterministic C# demo-data seeding.
- Season reports and CSV exports.

## Local demo

```bash
cp .env.example .env   # optional; every default works locally
docker compose up --build
docker compose run --rm seeder
```

## Demo logins

Owner: `owner@fieldledger.demo` / `FieldLedgerDemo!2026`  
Agronomist: `agronomist@fieldledger.demo` / `FieldLedgerDemo!2026`  
Viewer: `viewer@fieldledger.demo` / `FieldLedgerDemo!2026`

## RLS walkthrough

The API issues and validates its own JWTs, then forwards the verified user id into Postgres per request as `app.user_id` inside a transaction. RLS policies use that id to check organization membership and role. This means tenant isolation is enforced in the database itself: even a buggy API handler cannot return cross-org rows, because the API's database role cannot bypass RLS.
