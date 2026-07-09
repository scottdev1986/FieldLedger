---
title: Demo Script
status: accepted
tags: [fieldledger, demo]
last_updated: 2026-07-09
related: [seeder-demo-data, billing-entitlements, auth-rls]
---

# Demo Script

This is the two-minute portfolio walkthrough.

## Setup

```bash
docker compose up --build
```

Seed demo data:

```bash
docker compose run --rm seeder
```

## Demo credentials

| Email | Password | Role |
|---|---|---|
| `owner@fieldledger.demo` | `FieldLedgerDemo!2026` | Owner |
| `agronomist@fieldledger.demo` | `FieldLedgerDemo!2026` | Agronomist |
| `viewer@fieldledger.demo` | `FieldLedgerDemo!2026` | Viewer |

## Walkthrough

1. Log in as `owner@fieldledger.demo`.
2. Show organization switcher with two organizations.
3. Open `North Fork Farms`.
4. Show dashboard:
   - active fields,
   - acreage,
   - current season progress,
   - recent activity,
   - plan badge.
5. Open field detail.
6. Show activity timeline and per-season yield.
7. Open insights.
8. Show yield/acre and input cost vs harvest value.
9. Open members.
10. Show owner/agronomist/viewer roles.
11. Log out and log in as viewer.
12. Attempt to create/edit an activity.
13. Show UI denies it and API/RLS prevents it.
14. Return to owner login.
15. Show Free plan field limit.
16. Run the `psql` RLS simulation as that user against an unrelated org; result is zero rows.
17. Open the Billing page and click "Upgrade to Pro"; the plan badge flips instantly.
18. Show the plan-change history with the new audit entry.
19. Unarchive/create the fourth field now that the Free limit no longer applies.
20. Generate season report.
21. Export CSV.

## psql RLS moment

Run [`templates/rls-demo.sql`](../templates/rls-demo.sql) inside the database container:

```bash
docker compose exec db psql -U postgres -d fieldledger -f /path/to/rls-demo.sql
```

Talking point:

> Even if an API handler had a bug, the database still refuses cross-org rows because the tenant boundary is encoded in RLS policies.

## Closing line

> This is the public version of the kind of ag-operations reporting platform I have built professionally: orgs, roles, plan gating, reports, deterministic demo data, C# API work, and Postgres RLS behind the API — and no external services are required to run it.
