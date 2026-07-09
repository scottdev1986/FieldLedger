---
title: Domain Model
status: accepted
tags: [fieldledger, domain, tenancy]
last_updated: 2026-07-09
related: [database-schema, auth-rls, billing-entitlements]
---

# Domain Model

FieldLedger models a farm business as an organization that owns fields, crop seasons, activity logs, members, roles, and plan state.

See [data-model.mmd](../diagrams/data-model.mmd).

## Entity hierarchy

```text
organization
  ├── organization_members
  │     └── user (first-party users table)
  ├── fields
  │     └── field_seasons
  │            └── activities
  ├── seasons
  ├── entitlements
  └── plan_changes
```

## Core entities

### User

A first-party account stored in the `users` table (owned by the API — there is no external identity provider, and no separate `profiles` table; display data lives on the user row).

Important fields:

- `id`
- `email` (citext, unique)
- `display_name`
- `password_hash` (PBKDF2; never plaintext)
- `created_at`

### Organization

A farm business tenant.

Important fields:

- `id`
- `name`
- `slug`
- `created_by`
- `archived_at`

Business rules:

- Users only see organizations where they are active members.
- Owner role is required for plan changes and member management.
- Organization is the tenant root for RLS.

### Organization member

Connects a user to an organization.

Roles:

| Role | Purpose |
|---|---|
| `owner` | Plan changes, invites, role management, all data actions. |
| `agronomist` | Edit farm operations data. |
| `viewer` | Read-only access. |

### Field

A physical or operational field.

Important fields:

- `organization_id`
- `name`
- `acreage`
- `default_crop`
- `archived_at`

Business rules:

- Free orgs may have at most 3 active fields.
- Pro orgs may have unlimited active fields.
- Field names are unique within an organization.

### Season

A crop/accounting period for reporting.

Important fields:

- `organization_id`
- `year`
- `name`
- `starts_on`
- `ends_on`

Business rules:

- A year is unique within an organization.
- Seasons support cross-season insights.

### Field season

Links a field to a season and crop.

Purpose:

- Supports crop rotation.
- Stores seasonal crop choice and yield targets.
- Allows one field to have different crops in different years.

### Activity

A logged farm operation.

Types:

- `planting`
- `spraying`
- `irrigation`
- `fertilizer`
- `harvest`
- `note`

Important fields:

- `organization_id`
- `field_id`
- `season_id`
- `activity_type`
- `activity_date`
- `quantity`
- `quantity_unit`
- `cost_amount`
- `revenue_amount`
- `notes`
- `created_by`

Business rules:

- Owner and agronomist can create/edit activities.
- Viewer can only read activities.
- Harvest activities drive yield and harvest-value reporting.

### Entitlement and plan change

`entitlements` stores each organization's current plan (Free/Pro) and `updated_by`. `plan_changes` is an append-only audit trail of upgrades/downgrades (`from_plan`, `to_plan`, `changed_by`, `changed_at`). Both change only through the `security definer` function `app.set_org_plan`; `authenticated` has no direct write grants. See [[billing-entitlements]].

## Role capability matrix

| Capability | Owner | Agronomist | Viewer |
|---|---:|---:|---:|
| View organization dashboard | Yes | Yes | Yes |
| View field details | Yes | Yes | Yes |
| View insights | Yes | Yes | Yes |
| Create/edit fields | Yes | Yes | No |
| Archive fields | Yes | Yes | No |
| Create/edit seasons | Yes | Yes | No |
| Create/edit activity logs | Yes | Yes | No |
| Export CSV | Pro only | Pro only | No |
| Generate season report | Pro only | Pro only | Pro only |
| Invite members | Yes | No | No |
| Change member roles | Yes | No | No |
| Manage plan (upgrade/downgrade) | Yes | No | No |

## Tenant isolation rule

Every tenant-owned table includes `organization_id`. RLS policies call helper functions to determine whether the current user (from `app.user_id`) is an active organization member and whether their role allows the attempted operation.

This makes the organization membership table the central authorization primitive.
