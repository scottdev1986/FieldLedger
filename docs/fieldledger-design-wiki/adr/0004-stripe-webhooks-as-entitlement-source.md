---
title: ADR 0004 - Stripe Webhooks as Entitlement Source
status: superseded
last_updated: 2026-07-09
tags: [adr, stripe, entitlements]
related: [billing-entitlements]
---

# ADR 0004: Stripe Webhooks as Entitlement Source

## Status

Superseded by [ADR 0007](0007-in-app-plan-management-no-external-billing.md) on
2026-07-09. Stripe is removed so the app runs with no external services; Free/Pro
entitlement gating remains, managed through an in-app, owner-only server-side path.

## Context

Subscription state must not be trusted from the browser after Checkout. The browser can be redirected back to the app before asynchronous payment/subscription events fully settle.

## Decision

Treat verified Stripe webhooks as the source of truth for entitlement changes. The API creates Checkout sessions, but the entitlement flips only after webhook verification and database update.

## Consequences

Positive:

- Demonstrates production-grade subscription gating.
- Avoids trusting browser query params or local state.
- Makes local demo compelling through `stripe-cli` forwarding.

Tradeoffs:

- Webhook signature handling requires raw body access.
- Events must be idempotent.
- The UI needs a refetch/polling state after Checkout.
