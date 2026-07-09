---
title: Testing and CI
status: proposed
tags: [fieldledger, testing, ci, rls]
last_updated: 2026-07-09
related: [auth-rls, api-design, billing-entitlements]
---

# Testing and CI

The test suite should prove the project’s hard claims: tenant isolation, role enforcement, entitlement enforcement, first-party auth correctness, and deterministic demo data.

## Test layers

| Layer | Purpose |
|---|---|
| Unit tests | Pure business logic, generators, validators, entitlement rules. |
| API integration tests | Endpoint behavior, auth handling, role errors, plan-management flows. |
| Database/RLS tests | Direct policy verification with simulated `app.user_id` context. |
| Seeder tests | Deterministic data generation and idempotency. |
| Frontend tests | Role-aware rendering and critical flows. |
| E2E smoke tests | Login, dashboard, edit denial, upgrade, report generation. |

## RLS test matrix

| Scenario | Expected result |
|---|---|
| Owner reads own org | Allowed |
| Owner reads second org they belong to | Allowed |
| Owner reads unrelated org | Zero rows |
| Agronomist creates activity | Allowed |
| Agronomist manages billing | Denied |
| Viewer reads field | Allowed |
| Viewer creates activity | Denied |
| Viewer exports CSV | Denied |
| Free org creates first 3 fields | Allowed |
| Free org creates 4th field | Denied |
| Pro org creates 4th field | Allowed |
| Plan change via `app.set_org_plan` as owner | Allowed |
| Direct update of `entitlements` as `authenticated` | Denied |
| `app.set_org_plan` as non-owner | Exception |

## Auth tests

Minimum auth tests:

- register creates a user and rejects duplicate email,
- login with correct credentials returns `{ accessToken, user }`,
- login with wrong password returns `401`,
- login with unknown email returns `401`,
- expired token returns `401`,
- invalid/tampered token returns `401`,
- `GET /api/auth/me` returns the current user profile and org memberships.

## API tests

Minimum API tests:

- missing token returns `401`,
- invalid token returns `401`,
- unauthorized org returns `404` or `403`,
- viewer cannot mutate activity,
- agronomist can mutate activity,
- Free field limit returns useful error,
- Pro export succeeds,
- Free export fails.

## Plan-management tests

Minimum plan-management tests:

- owner can `POST /api/orgs/{orgId}/billing/upgrade` and the org flips to Pro,
- agronomist upgrade attempt returns `403`,
- viewer upgrade attempt returns `403`,
- downgrade with more than 3 active fields returns `422` (`too_many_active_fields_for_free`),
- every plan change writes a `plan_changes` audit row,
- Pro enables CSV export and season report; Free blocks both.

## Seeder tests

Minimum seeder tests:

- deterministic generator returns same output for same seed,
- different org/field/year creates different output,
- generated dates fall within expected crop windows,
- generated costs/revenue are non-negative,
- rerun does not duplicate organizations, fields, seasons, or memberships.

## CI jobs

See [`templates/github-actions-ci.example.yml`](../templates/github-actions-ci.example.yml).

Recommended jobs:

1. Web install/typecheck/lint/build.
2. API restore/build/test.
3. Seeder restore/build/test.
4. Database job: run a `postgres:17` service container, apply `db/init` roles plus `db/migrations`, then run RLS policy assertions.
5. Optional Playwright e2e smoke test — now fully possible in CI because the stack has no external services.

## CI success definition

A pull request is healthy when:

- web builds,
- API builds,
- tests pass,
- database policies pass,
- no generated demo data is nondeterministic,
- no secret-like values are committed.
