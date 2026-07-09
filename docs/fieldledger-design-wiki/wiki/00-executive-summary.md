---
title: FieldLedger Executive Summary
status: accepted
tags: [fieldledger, overview, saas]
last_updated: 2026-07-09
related: [architecture, auth-rls, billing-entitlements]
---

# FieldLedger Executive Summary

FieldLedger is a multi-tenant farm operations SaaS for tracking fields, seasons, crop activity, cost, yield, members, roles, plan state, and plan-gated reporting.

The project exists as a public portfolio artifact: a readable counterpart to private/gated agricultural operations reporting work. It should look and behave like a serious SaaS MVP, while staying compact enough to build and demo locally with zero external services.

## Primary technical claim

The core differentiator is not merely "Next.js + Postgres." The stronger design is:

> The browser authenticates against the API's first-party auth endpoints, the ASP.NET Core Web API issues and validates its own JWT, forwards verified user context into Postgres per request, and Postgres RLS enforces organization isolation and role rights even though traffic goes through the C# API.

This makes RLS a defense-in-depth layer behind the API, not a replacement for the API.

See [[auth-rls]] and [ADR 0006](../adr/0006-self-contained-postgres-first-party-auth.md).

## Product scope

A farm business can:

- create organizations,
- invite members,
- assign `owner`, `agronomist`, or `viewer` roles,
- create fields,
- track crop seasons,
- log planting, spraying, irrigation, fertilizer, harvest, and notes,
- view yield and cost insights,
- upgrade from Free to Pro,
- export CSV data,
- generate a season report.

## Plans

| Plan | Field limit | CSV export | Season report |
|---|---:|---:|---:|
| Free | 3 active fields | No | No |
| Pro | Unlimited | Yes | Yes |

The UI may show plan-aware affordances, but browser plan state is never trusted. Entitlements are enforced server-side by the API and backed by database constraints/triggers where practical. Plan changes are an in-app, owner-only action — no payment is processed (see [ADR 0007](../adr/0007-in-app-plan-management-no-external-billing.md)).

## Stack

| Layer | Choice | Purpose |
|---|---|---|
| Web | Next.js / React / Tailwind | Auth UI, dashboard, charts, plan management UX |
| API | ASP.NET Core Web API (.NET 10, minimal APIs) | First-party auth, JWT issuance/validation, business operations, reports |
| Database | PostgreSQL 17 container, RLS enabled + forced | Tenancy, authorization, relational data |
| Auth | First-party email/password, PBKDF2, API-issued HS256 JWTs | Identity owned by the API |
| Plans | Free/Pro entitlements table, in-app owner-only upgrade | Server-side gating without a payment provider |
| Seeding | C# console app (direct Npgsql) | Demo users, orgs, deterministic farm data |
| Migrations | Plain SQL in `db/migrations`, applied by the seeder binary in `migrate` mode | Repeatable schema setup |
| Local orchestration | Docker Compose | `db`, `migrate`, `api`, `web`, `seeder` |

## Goals

1. Demonstrate a complete multi-tenant SaaS MVP.
2. Demonstrate C# backend architecture in a modern web product.
3. Demonstrate PostgreSQL RLS policy design behind an API layer.
4. Demonstrate server-side plan/entitlement gating with a clean seam for a real payment provider.
5. Provide deterministic demo data with realistic agricultural activity.
6. Make the project demoable from zero using Compose plus seeding.
7. Make the README and docs portfolio-ready.

## Non-goals

- No production agronomic recommendations.
- No GIS boundary editing.
- No offline-first mobile app.
- No real payment processing in v1.
- No external identity provider in v1.
- No full accounting, inventory, compliance, or equipment-maintenance system.
- No multi-region production deployment design in v1.

## Definition of done

FieldLedger is done when `docker compose up --build` and `docker compose run --rm seeder` produce a full local demo with no external accounts, keys, or internet services: demo users, two organizations, three seasons of deterministic data, working role boundaries, working Free/Pro gating with an owner-only in-app upgrade, an RLS walkthrough, and green CI.
