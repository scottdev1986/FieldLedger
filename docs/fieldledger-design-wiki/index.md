---
title: FieldLedger Design Wiki Index
status: accepted
tags: [fieldledger, index]
last_updated: 2026-07-09
related: [00-executive-summary, architecture]
---

# FieldLedger Design Wiki Index

## Recommended reading path

1. [Executive Summary](wiki/00-executive-summary.md)
2. [Architecture](wiki/architecture.md)
3. [Domain Model](wiki/domain-model.md)
4. [Auth and RLS](wiki/auth-rls.md)
5. [Plans and Entitlements](wiki/billing-entitlements.md)
6. [Seeder and Demo Data](wiki/seeder-demo-data.md)
7. [Demo Script](wiki/demo-script.md)
8. [Acceptance Criteria](wiki/acceptance-criteria.md)

## Core design pages

| Page | Purpose |
|---|---|
| [Executive Summary](wiki/00-executive-summary.md) | Product purpose, goals, non-goals, and completion definition. |
| [Architecture](wiki/architecture.md) | Service boundaries, runtime flows, and deployment assumptions. |
| [Domain Model](wiki/domain-model.md) | Organizations, members, roles, fields, seasons, activities, and business rules. |
| [Database Schema](wiki/database-schema.md) | PostgreSQL table design, enums, indexes, and migration ordering. |
| [Auth and RLS](wiki/auth-rls.md) | First-party JWT auth in the API, transaction-scoped claim forwarding, RLS policies, and defense-in-depth model. |
| [Plans and Entitlements](wiki/billing-entitlements.md) | Free/Pro plan state, in-app owner-only plan changes, audit trail, and server-side gating. |
| [API Design](wiki/api-design.md) | ASP.NET Core endpoint map, middleware, errors, and response contracts. |
| [Frontend Design](wiki/frontend-design.md) | React/Next.js page structure, state management, and role-aware UI behavior. |
| [Seeder and Demo Data](wiki/seeder-demo-data.md) | C# seeder design, demo accounts, deterministic crop-calendar generator, and idempotency. |
| [Reporting and Insights](wiki/reporting-insights.md) | Dashboard metrics, charts, CSV export, and Pro-gated season report. |
| [Docker and Local Dev](wiki/docker-local-dev.md) | Compose services, environment variables, and local commands. |
| [Testing and CI](wiki/testing-ci.md) | Unit, integration, RLS, billing, and e2e test plan. |
| [Risks, Assumptions, and Open Questions](wiki/risks-assumptions-open-questions.md) | Known risks, mitigations, assumptions, and unresolved decisions. |
| [Demo Script](wiki/demo-script.md) | Two-minute portfolio demo flow. |
| [Acceptance Criteria](wiki/acceptance-criteria.md) | Done-when checklist. |
| [Build Plan](wiki/build-plan.md) | Recommended implementation sequence. |

## Architecture decision records

| ADR | Decision |
|---|---|
| [ADR 0001: Public Twin Portfolio SaaS](adr/0001-public-twin-portfolio-saas.md) | Build a public, non-gated agricultural SaaS artifact to demonstrate professional category fit. |
| [ADR 0002: ASP.NET API with Supabase RLS](adr/0002-use-aspnet-api-with-supabase-rls.md) | Superseded by ADR 0006. |
| [ADR 0003: Transaction-Scoped Claim Forwarding](adr/0003-transaction-scoped-claim-forwarding.md) | Use `SET LOCAL`-style per-request database context for RLS. |
| [ADR 0004: Stripe Webhooks as Entitlement Source](adr/0004-stripe-webhooks-as-entitlement-source.md) | Superseded by ADR 0007. |
| [ADR 0005: Deterministic C# Seeder](adr/0005-deterministic-csharp-seeder.md) | Use a C# seeder service to create repeatable demo data. |
| [ADR 0006: Self-Contained Postgres with First-Party Auth](adr/0006-self-contained-postgres-first-party-auth.md) | Local PostgreSQL container and API-issued JWTs replace Supabase; RLS stays the final boundary. |
| [ADR 0007: In-App Plan Management](adr/0007-in-app-plan-management-no-external-billing.md) | Stripe removed; Free/Pro gating kept via an owner-only, audited server-side plan-change path. |

## Diagrams

| Diagram | Purpose |
|---|---|
| [Architecture Mermaid](diagrams/architecture.mmd) | Service topology. |
| [Auth/RLS Sequence](diagrams/auth-rls-sequence.mmd) | Browser → API → database authorization flow. |
| [Billing Sequence](diagrams/billing-sequence.mmd) | In-app plan change (upgrade/downgrade) flow. |
| [Data Model ERD](diagrams/data-model.mmd) | Core relational model. |

## Templates

| Template | Purpose |
|---|---|
| [`.env.example`](templates/.env.example) | Local environment variable skeleton. |
| [`docker-compose.example.yml`](templates/docker-compose.example.yml) | Compose service skeleton. |
| [`rls-demo.sql`](templates/rls-demo.sql) | psql RLS simulation script. |
| [`migration-skeleton.sql`](templates/migration-skeleton.sql) | Initial migration outline. |
| [`github-actions-ci.example.yml`](templates/github-actions-ci.example.yml) | CI skeleton. |
| [`api-claim-forwarding.example.cs`](templates/api-claim-forwarding.example.cs) | ASP.NET Core/Npgsql request context sketch. |
| [`README-portfolio-copy.md`](templates/README-portfolio-copy.md) | README positioning copy. |

## References

| Reference | Purpose |
|---|---|
| [Wiki Map](references/wiki-map.md) | Task-to-page routing guide for agents and maintainers. |

## Source and print views

| File | Purpose |
|---|---|
| [Raw Product Brief](raw/fieldledger-product-brief.md) | Original product/design prompt preserved as source material. |
| [Source Map](docs/source-map.md) | Official implementation documentation links. |
| [Single-file TDD](printable/fieldledger-technical-design-single-file.md) | Flat printable version of the design. |
