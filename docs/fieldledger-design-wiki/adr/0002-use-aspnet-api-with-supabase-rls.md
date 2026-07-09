---
title: ADR 0002 - Use ASP.NET API with Supabase RLS
status: superseded
last_updated: 2026-07-09
tags: [adr, security, rls, aspnet]
related: [auth-rls, architecture]
---

# ADR 0002: Use ASP.NET API with Supabase RLS

## Status

Superseded by [ADR 0006](0006-self-contained-postgres-first-party-auth.md) on 2026-07-09.
The core decision (ASP.NET Core API in front of Postgres with RLS as the final
authorization boundary) carries forward; the Supabase-hosted database and Supabase Auth
are replaced by a local Postgres container and first-party JWT auth.

## Context

A client-direct Supabase app can show RLS, but it does not show enough C# backend architecture. A pure API app can show C#, but often loses the Supabase RLS story if the API uses elevated DB credentials.

## Decision

Use an ASP.NET Core Web API for all application data traffic while preserving Postgres RLS as the final authorization layer.

The API validates Supabase JWTs and forwards the verified user context into Postgres for each transaction.

## Consequences

Positive:

- Demonstrates C# API work.
- Demonstrates RLS defense-in-depth.
- Prevents browser-direct data access in v1.
- Makes takeover/audit story stronger because RLS remains the tenant landmine mitigation.

Tradeoffs:

- Claim forwarding must be implemented carefully.
- Connection pooling creates state-leak risks if transaction-scoped settings are not used.
- Tests must verify both API authorization and database policies.
