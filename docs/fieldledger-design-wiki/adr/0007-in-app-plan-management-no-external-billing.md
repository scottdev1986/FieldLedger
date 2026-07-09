---
title: ADR 0007 - In-App Plan Management Without External Billing
status: accepted
last_updated: 2026-07-09
tags: [adr, entitlements, plans]
related: [billing-entitlements, api-design]
---

# ADR 0007: In-App Plan Management Without External Billing

## Status

Accepted. Supersedes [ADR 0004](0004-stripe-webhooks-as-entitlement-source.md).

## Context

ADR 0004 made verified Stripe webhooks the source of truth for entitlement changes. That
required a Stripe test-mode account and a `stripe-cli` forwarding container, which
conflicts with the new requirement that FieldLedger run with no external services.

Dropping plans entirely would gut a core demo point: server-side entitlement gating
(field limits, CSV export, season reports) enforced in the API and the database.

## Decision

Keep the Free/Pro plan model and every server-side enforcement point, but make plan
changes an in-app, owner-only action:

- `POST /api/orgs/{orgId}/billing/upgrade` and `.../downgrade` are the only mutation paths.
- Plan changes execute through a `security definer` database function
  (`app.set_org_plan`) that verifies the caller is an org owner, updates `entitlements`,
  and appends an immutable `plan_changes` audit row. `authenticated` has no direct write
  grants on entitlement tables.
- No payment is processed and the UI never claims one is. The README notes that a real
  deployment would slot a payment provider behind this same entitlement seam: the
  provider's webhook handler would call the same plan-change path.

## Consequences

Positive:

- Demo works offline; the upgrade moment is instant and reliable.
- Entitlement enforcement (API checks, DB trigger for field limits, gated endpoints)
  remains fully demonstrable and testable.
- The audit table preserves the "entitlements only change through a controlled server-side
  path" story.

Tradeoffs:

- No demonstration of webhook signature verification or event idempotency (that story is
  now told in the README as the seam design).
- "Billing" is simulated; endpoint and route names keep the `/billing` shape for realism,
  but prose calls the feature plans and entitlements.

See [[billing-entitlements]] for the updated flow.
