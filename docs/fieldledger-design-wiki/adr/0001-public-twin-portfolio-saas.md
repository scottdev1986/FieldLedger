---
title: ADR 0001 - Public Twin Portfolio SaaS
status: accepted
last_updated: 2026-07-09
tags: [adr, portfolio, product]
related: [00-executive-summary]
---

# ADR 0001: Public Twin Portfolio SaaS

## Status

Accepted.

## Context

The original agricultural operations reporting work is gated/private. A public artifact is needed to demonstrate comparable architecture and domain fluency without exposing private code or customer data.

## Decision

Build FieldLedger as a public farm operations SaaS MVP with orgs, roles, billing, RLS, deterministic demo data, dashboards, and reports.

## Consequences

Positive:

- Shows SaaS MVP architecture.
- Shows C# backend capability.
- Shows Supabase RLS policy design.
- Shows Stripe subscription gating.
- Creates a concrete demo for takeover/audit/MVP work.

> Note (2026-07-09): the decision stands, but after
> [ADR 0006](0006-self-contained-postgres-first-party-auth.md) and
> [ADR 0007](0007-in-app-plan-management-no-external-billing.md) the demonstrated skills
> are PostgreSQL RLS design (local container) and server-side plan/entitlement gating
> without an external billing provider.

Tradeoffs:

- The product scope must remain compact.
- Demo realism must be balanced against build time.
- README/demo polish matters as much as code completeness.
