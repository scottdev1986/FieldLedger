---
title: Plans and Entitlements
status: accepted
tags: [fieldledger, plans, entitlements]
last_updated: 2026-07-09
related: [architecture, domain-model, api-design]
---

# Plans and Entitlements

FieldLedger keeps a Free/Pro plan model to demonstrate server-side entitlement gating, but plan changes are an in-app, owner-only action. No payment is processed and the UI never claims one is. See [ADR 0007](../adr/0007-in-app-plan-management-no-external-billing.md).

See [billing-sequence.mmd](../diagrams/billing-sequence.mmd).

## Plans

| Plan | Field limit | CSV export | Season report |
|---|---:|---:|---:|
| Free | 3 active fields | Disabled | Disabled |
| Pro | Unlimited | Enabled | Enabled |

## Entitlement rule

The browser may display the current plan, but only the API and database enforce it.

Entitlements are checked at:

1. API command handlers.
2. Report/export endpoints.
3. Database field-limit guardrail.
4. RLS/grants for direct table access.

## Plan-change flow

Upgrade:

```text
Owner clicks Upgrade
  -> web calls POST /api/orgs/{orgId}/billing/upgrade (Bearer JWT)
  -> API opens the normal user transaction (set local role + app.user_id context)
  -> API calls app.set_org_plan(orgId, 'pro')
  -> function verifies app.can_manage_org(orgId), updates entitlements,
     inserts a plan_changes audit row
  -> commit; API responds 200 with the new plan
  -> web refetches billing state and dashboard; badge flips instantly
```

Downgrade:

```text
Owner clicks Downgrade
  -> web calls POST /api/orgs/{orgId}/billing/downgrade
  -> if the org has more than 3 active fields, the API returns
     422 too_many_active_fields_for_free telling the owner to archive fields first
  -> otherwise app.set_org_plan(orgId, 'free') runs as above
```

No polling loop is needed: the change is synchronous, so the web app simply refetches after the mutation resolves.

## Plan-change function

Plan changes execute only through a `security definer` function:

```sql
app.set_org_plan(p_org_id uuid, p_plan plan_tier)
```

It verifies `app.can_manage_org(p_org_id)`, updates the `entitlements` row, and inserts a `plan_changes` audit row. Direct writes to `entitlements`/`plan_changes` are not granted to `authenticated`, preserving the "entitlements only change through a controlled server-side path" property.

## Entitlement state

`entitlements` has no Stripe columns in v2: `source_stripe_event_id` is dropped and `updated_by uuid references users(id)` is added, so every entitlement row records which user last changed it.

## Plan-change audit table

```sql
create table plan_changes (
  id uuid primary key default gen_random_uuid(),
  organization_id uuid not null references organizations(id) on delete cascade,
  from_plan plan_tier not null,
  to_plan plan_tier not null,
  changed_by uuid not null references users(id),
  changed_at timestamptz not null default now()
);
```

Rows are append-only and surfaced as the plan-change history on the billing page.

## Billing read endpoint

`GET /api/orgs/{orgId}/billing` returns everything the billing page needs:

```json
{
  "plan": "free",
  "limits": {
    "maxFields": 3,
    "csvExportEnabled": false,
    "seasonReportEnabled": false
  },
  "usage": {
    "activeFieldCount": 3
  },
  "history": [
    {
      "fromPlan": "pro",
      "toPlan": "free",
      "changedBy": "owner@fieldledger.demo",
      "changedAt": "2026-07-09T14:00:00Z"
    }
  ]
}
```

## Free field limit

The API checks the limit before creating a field, but the database also enforces it with the field-limit trigger (unchanged from v1), so even a buggy handler cannot exceed the plan.

Conceptual trigger behavior:

```sql
if entitlement.max_fields is not null and active_field_count >= entitlement.max_fields then
  raise exception 'Field limit reached for current plan';
end if;
```

## Payment-provider seam

A real deployment would slot a payment provider behind this same entitlement seam: the provider's webhook handler would call the same plan-change path (`app.set_org_plan`) that the in-app action uses today. That seam is the point of [ADR 0007](../adr/0007-in-app-plan-management-no-external-billing.md). Prose calls this feature "plans and entitlements"; the `/billing` route and endpoint names keep the billing shape for realism.
