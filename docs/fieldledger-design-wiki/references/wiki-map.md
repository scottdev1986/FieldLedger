---
title: FieldLedger Wiki Map
status: accepted
tags: [fieldledger, reference]
last_updated: 2026-07-09
related: [architecture, build-plan]
---

# FieldLedger Wiki Map

Use this routing map after reading [index.md](../index.md) and before opening topic pages. Prefer the smallest page set that answers the task.

## Task Routing

| Task | Primary pages | Supporting pages |
|---|---|---|
| Understand product scope, goals, or non-goals | [Executive Summary](../wiki/00-executive-summary.md) | [Acceptance Criteria](../wiki/acceptance-criteria.md) |
| Plan or scaffold the repository | [Build Plan](../wiki/build-plan.md), [Architecture](../wiki/architecture.md) | [Docker and Local Dev](../wiki/docker-local-dev.md), [Testing and CI](../wiki/testing-ci.md) |
| Build or review local Docker setup | [Docker and Local Dev](../wiki/docker-local-dev.md) | [Architecture](../wiki/architecture.md), [Seeder and Demo Data](../wiki/seeder-demo-data.md), [Plans and Entitlements](../wiki/billing-entitlements.md) |
| Implement API routes or error contracts | [API Design](../wiki/api-design.md) | [Auth and RLS](../wiki/auth-rls.md), [Plans and Entitlements](../wiki/billing-entitlements.md), [Reporting and Insights](../wiki/reporting-insights.md) |
| Implement authentication, authorization, or RLS | [Auth and RLS](../wiki/auth-rls.md) | [Database Schema](../wiki/database-schema.md), [API Design](../wiki/api-design.md), [ADR 0006](../adr/0006-self-contained-postgres-first-party-auth.md), [ADR 0003](../adr/0003-transaction-scoped-claim-forwarding.md) |
| Implement database migrations or policies | [Database Schema](../wiki/database-schema.md), [Auth and RLS](../wiki/auth-rls.md) | [Domain Model](../wiki/domain-model.md), [Plans and Entitlements](../wiki/billing-entitlements.md) |
| Implement frontend routes, shell, or state management | [Frontend Design](../wiki/frontend-design.md) | [API Design](../wiki/api-design.md), [Domain Model](../wiki/domain-model.md), [Plans and Entitlements](../wiki/billing-entitlements.md) |
| Implement seeding or demo data | [Seeder and Demo Data](../wiki/seeder-demo-data.md) | [Domain Model](../wiki/domain-model.md), [Database Schema](../wiki/database-schema.md), [ADR 0005](../adr/0005-deterministic-csharp-seeder.md) |
| Implement plans, entitlements, or plan gating | [Plans and Entitlements](../wiki/billing-entitlements.md) | [API Design](../wiki/api-design.md), [Database Schema](../wiki/database-schema.md), [ADR 0007](../adr/0007-in-app-plan-management-no-external-billing.md) |
| Implement reporting, insights, exports, or season reports | [Reporting and Insights](../wiki/reporting-insights.md) | [API Design](../wiki/api-design.md), [Frontend Design](../wiki/frontend-design.md), [Plans and Entitlements](../wiki/billing-entitlements.md) |
| Define or verify tests and CI | [Testing and CI](../wiki/testing-ci.md), [Acceptance Criteria](../wiki/acceptance-criteria.md) | [Auth and RLS](../wiki/auth-rls.md), [Docker and Local Dev](../wiki/docker-local-dev.md) |
| Prepare portfolio demo copy or demo flow | [Demo Script](../wiki/demo-script.md), [Acceptance Criteria](../wiki/acceptance-criteria.md) | [Executive Summary](../wiki/00-executive-summary.md), [README Portfolio Copy](../templates/README-portfolio-copy.md) |
| Triage risks, assumptions, or design gaps | [Risks, Assumptions, and Open Questions](../wiki/risks-assumptions-open-questions.md) | Relevant ADRs and focused topic pages |

## Template Routing

| Need | Template |
|---|---|
| Local environment variables | [`.env.example`](../templates/.env.example) |
| Docker Compose services | [`docker-compose.example.yml`](../templates/docker-compose.example.yml) |
| CI jobs | [`github-actions-ci.example.yml`](../templates/github-actions-ci.example.yml) |
| Initial SQL migration shape | [`migration-skeleton.sql`](../templates/migration-skeleton.sql) |
| psql RLS simulation | [`rls-demo.sql`](../templates/rls-demo.sql) |
| ASP.NET claim forwarding sketch | [`api-claim-forwarding.example.cs`](../templates/api-claim-forwarding.example.cs) |
| README positioning copy | [`README-portfolio-copy.md`](../templates/README-portfolio-copy.md) |

## Conflict Resolution

When pages conflict, prefer accepted ADRs and accepted wiki pages over proposed pages. If implementation work reveals a durable design change, update the smallest relevant wiki page and append an entry to [log.md](../log.md).
