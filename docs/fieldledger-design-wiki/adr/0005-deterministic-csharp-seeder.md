---
title: ADR 0005 - Deterministic C# Seeder
status: accepted
last_updated: 2026-07-09
tags: [adr, seeding, demo-data]
related: [seeder-demo-data]
---

# ADR 0005: Deterministic C# Seeder

## Status

Accepted.

## Context

A portfolio SaaS needs a credible demo without manual data entry. Random-looking but unstable data makes screenshots, tests, and demos harder to reason about.

## Decision

Build a C# console seeder service that creates demo users/orgs/roles and deterministic crop-calendar data.

## Consequences

Positive:

- Demo works from zero.
- Data looks realistic.
- Tests can assert generated values.
- Shows C# outside the API as well.

Tradeoffs:

- Seeder needs elevated credentials.
- Idempotency must be designed explicitly.
- Generated data should not pretend to be agronomic advice.
