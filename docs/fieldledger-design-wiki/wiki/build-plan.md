---
title: Build Plan
status: proposed
tags: [fieldledger, implementation-plan]
last_updated: 2026-07-09
related: [acceptance-criteria, testing-ci]
---

# Build Plan

Build FieldLedger in layers. Each phase should end with a runnable state.

## Phase 0: Repository scaffold

- Create monorepo structure.
- Add Docker Compose skeleton.
- Add `.env.example`.
- Add README stub.
- Add CI skeleton.

Exit criteria:

- Empty web/API/seeder projects build in CI.

## Phase 1: Database schema and migrations

- Stand up the local `postgres:17` container.
- Add `db/init` role bootstrap (`authenticated`, `fieldledger_api`, `fieldledger_migrator`).
- Create enums and tables, including `users`, `entitlements`, and `plan_changes`.
- Add indexes.
- Add RLS helper functions.
- Enable and force RLS.
- Add initial grants.

Exit criteria:

- Migrations apply cleanly against a fresh container.
- Basic RLS simulation script runs via `psql`.

## Phase 2: API auth and RLS context

- Implement first-party register/login/me endpoints.
- Add JWT issuance and validation (HS256, `AUTH_JWT_SECRET`).
- Implement DB session abstraction with transaction-scoped `app.user_id` forwarding.
- Add `/health` endpoint.

Exit criteria:

- Authenticated API request can read only visible organizations through RLS.

## Phase 3: Core SaaS endpoints

- Organizations.
- Members.
- Fields.
- Seasons.
- Activities.
- Dashboard aggregates.

Exit criteria:

- Owner/agronomist/viewer role matrix works via API tests.

## Phase 4: Seeder

- Create demo users directly via Npgsql with API-compatible password hashes.
- Create organizations/memberships.
- Generate deterministic seasons and activities.
- Add idempotency marker.
- Implement `migrate` mode used by the one-shot `migrate` compose service.

Exit criteria:

- `docker compose run --rm seeder` gives a full demo dataset.

## Phase 5: Frontend shell

- First-party login page (email/password plus demo-account buttons).
- App shell.
- Org switcher.
- Dashboard.
- Field detail.
- Members page.

Exit criteria:

- Demo users can log in and navigate according to role.

## Phase 6: Insights and reporting

- Insights API query.
- Charts.
- Season report preview.
- CSV export.
- PDF output if time allows.

Exit criteria:

- Seeded data produces credible charts and report output.

## Phase 7: Plans and entitlements

- `GET /api/orgs/{orgId}/billing` read endpoint (plan, limits, usage, plan-change history).
- Owner-only upgrade/downgrade endpoints backed by `app.set_org_plan`.
- Free field-limit guardrail (API check plus DB trigger).
- Gated exports/reports UI states (locked on Free, enabled on Pro).

Exit criteria:

- In-app upgrade flips org to Pro and unlocks gated features immediately.

## Phase 8: Polish and portfolio packaging

- README screenshots/GIF.
- RLS walkthrough via `psql`.
- Demo script.
- CI badge.
- Security notes.

Exit criteria:

- Project is demoable in two minutes from a fresh checkout.
