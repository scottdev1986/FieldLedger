---
title: Acceptance Criteria
status: accepted
tags: [fieldledger, done]
last_updated: 2026-07-09
related: [demo-script, testing-ci, build-plan]
---

# Acceptance Criteria

FieldLedger is complete when the following are true.

## Local setup

- [ ] `docker compose up --build` starts `db`, `migrate`, `api`, and `web`.
- [ ] `docker compose run --rm seeder` creates demo data from zero.
- [ ] No `.env` file is required; every variable has a working local default in `docker-compose.yml`, with the full variable reference kept in the wiki's `templates/.env.example`.
- [ ] README documents fully local setup — no external service setup section is needed.

## Demo users and data

- [ ] Owner login works.
- [ ] Agronomist login works.
- [ ] Viewer login works.
- [ ] Two demo organizations exist.
- [ ] Three seasons of field/activity data exist.
- [ ] Seeder is idempotent.

## Multi-tenancy and roles

- [ ] Users only see organizations where they are members.
- [ ] Cross-org API query returns no data.
- [ ] RLS simulation query returns zero unauthorized rows.
- [ ] Owner can manage members and billing.
- [ ] Agronomist can edit field/season/activity data.
- [ ] Agronomist cannot manage billing or members.
- [ ] Viewer can read but cannot mutate data.

## Plans and entitlements

- [ ] Free orgs are limited to 3 active fields.
- [ ] Pro orgs have unlimited active fields.
- [ ] Owner can upgrade and downgrade in-app; non-owners cannot.
- [ ] Plan changes are audited in `plan_changes`.
- [ ] Entitlement state updates server-side.
- [ ] UI reflects Pro immediately after upgrade.

## Reporting

- [ ] Insights page shows cross-season charts.
- [ ] CSV export is Pro-gated.
- [ ] Season report is Pro-gated.
- [ ] Report includes three years of seeded data.

## Documentation and CI

- [ ] README has architecture diagram.
- [ ] README has RLS walkthrough via `psql`.
- [ ] README has demo credentials.
- [ ] README has two-minute demo script.
- [ ] CI builds web.
- [ ] CI builds API.
- [ ] CI builds seeder.
- [ ] CI runs tests.
- [ ] CI includes RLS policy tests via a database job with a `postgres:17` service container.
