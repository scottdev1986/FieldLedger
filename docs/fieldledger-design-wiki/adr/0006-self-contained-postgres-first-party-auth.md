---
title: ADR 0006 - Self-Contained Postgres with First-Party JWT Auth
status: accepted
last_updated: 2026-07-09
tags: [adr, postgres, auth, security]
related: [auth-rls, architecture, docker-local-dev]
---

# ADR 0006: Self-Contained Postgres with First-Party JWT Auth

## Status

Accepted. Supersedes [ADR 0002](0002-use-aspnet-api-with-supabase-rls.md).

## Context

ADR 0002 placed the ASP.NET Core API in front of Supabase hosted Postgres, using Supabase
Auth as the identity provider. That design required two external hosted services (Supabase
project, plus Stripe per ADR 0004) before the demo could run.

The project goal changed: FieldLedger must run and demo completely locally with
`docker compose up --build` and no external accounts, API keys, or internet services.

## Decision

Replace the hosted Supabase project with a local PostgreSQL 17 container, and replace
Supabase Auth with first-party authentication owned by the ASP.NET Core API:

- The API stores users in its own `users` table with PBKDF2 password hashes
  (ASP.NET Core `PasswordHasher`).
- The API issues and validates its own HS256 JWTs (`AUTH_JWT_SECRET`, issuer
  `fieldledger-api`).
- Postgres RLS remains the final authorization boundary. The per-request
  transaction-scoped claim forwarding from [ADR 0003](0003-transaction-scoped-claim-forwarding.md)
  is unchanged, with the context key renamed from `request.jwt.claim.sub` to `app.user_id`
  since PostgREST compatibility is no longer needed.
- The API connects as `fieldledger_api`, a role without `BYPASSRLS`; elevated access is
  restricted to the migration/seeding role.

## Consequences

Positive:

- Zero external dependencies: clone, compose up, seed, demo.
- The core portfolio claim survives intact: C# API in front, Postgres RLS behind.
- Auth becomes demonstrable C# work (token issuance, hashing, middleware) instead of
  provider configuration.
- CI can run the full stack, including RLS and e2e tests, with a service container.

Tradeoffs:

- We own password hashing, token lifetime, and secret management (dev defaults are
  documented as local-only).
- No hosted-auth features (email verification, OAuth, MFA) in v1.
- Browser token storage in `localStorage` is an accepted portfolio-v1 tradeoff versus an
  httpOnly-cookie BFF pattern; documented in [[frontend-design]].

See [[auth-rls]], [[architecture]], and [[docker-local-dev]] for the updated design.
