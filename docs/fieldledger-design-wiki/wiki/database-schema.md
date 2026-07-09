---
title: Database Schema
status: proposed
tags: [fieldledger, postgres, schema]
last_updated: 2026-07-09
related: [domain-model, auth-rls, billing-entitlements]
---

# Database Schema

The schema is organized around organization tenancy. All tenant-owned tables include `organization_id`, and RLS policies use that column to enforce isolation. The database is a local PostgreSQL 17 container; identity lives in a first-party `users` table (see [[auth-rls]] and [ADR 0006](../adr/0006-self-contained-postgres-first-party-auth.md)).

## Extensions and enums

```sql
create extension if not exists pgcrypto;
create extension if not exists citext;

create type member_role as enum ('owner', 'agronomist', 'viewer');
create type plan_tier as enum ('free', 'pro');
create type activity_type as enum (
  'planting',
  'spraying',
  'irrigation',
  'fertilizer',
  'harvest',
  'note'
);
create type crop_type as enum ('corn', 'soybean', 'wheat');
```

## Users

First-party identity table. Replaces the v1 `auth.users` reference and the separate `profiles` table; display name and credential hash live here.

```sql
create table users (
  id uuid primary key default gen_random_uuid(),
  email citext not null unique,
  display_name text not null,
  password_hash text not null,
  created_at timestamptz not null default now()
);
```

`password_hash` is a PBKDF2 hash produced by ASP.NET Core `PasswordHasher`; see [[auth-rls]].

## Organizations

```sql
create table organizations (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  slug text not null unique,
  created_by uuid not null references users(id),
  created_at timestamptz not null default now(),
  archived_at timestamptz
);
```

## Organization members

```sql
create table organization_members (
  organization_id uuid not null references organizations(id) on delete cascade,
  user_id uuid not null references users(id) on delete cascade,
  role member_role not null,
  status text not null default 'active',
  created_at timestamptz not null default now(),
  primary key (organization_id, user_id)
);
```

## Fields

```sql
create table fields (
  id uuid primary key default gen_random_uuid(),
  organization_id uuid not null references organizations(id) on delete cascade,
  name text not null,
  acreage numeric(10,2) not null check (acreage > 0),
  default_crop crop_type not null,
  created_at timestamptz not null default now(),
  archived_at timestamptz,
  unique (organization_id, name)
);
```

## Seasons

```sql
create table seasons (
  id uuid primary key default gen_random_uuid(),
  organization_id uuid not null references organizations(id) on delete cascade,
  year int not null check (year between 2000 and 2100),
  name text not null,
  starts_on date not null,
  ends_on date not null,
  created_at timestamptz not null default now(),
  unique (organization_id, year),
  check (starts_on < ends_on)
);
```

## Field seasons

```sql
create table field_seasons (
  id uuid primary key default gen_random_uuid(),
  organization_id uuid not null references organizations(id) on delete cascade,
  field_id uuid not null references fields(id) on delete cascade,
  season_id uuid not null references seasons(id) on delete cascade,
  crop crop_type not null,
  target_yield_per_acre numeric(10,2),
  created_at timestamptz not null default now(),
  unique (field_id, season_id)
);
```

## Activities

```sql
create table activities (
  id uuid primary key default gen_random_uuid(),
  organization_id uuid not null references organizations(id) on delete cascade,
  field_id uuid not null references fields(id) on delete cascade,
  season_id uuid not null references seasons(id) on delete cascade,
  activity_type activity_type not null,
  activity_date date not null,
  quantity numeric(12,2),
  quantity_unit text,
  cost_amount numeric(12,2) not null default 0,
  revenue_amount numeric(12,2) not null default 0,
  notes text,
  created_by uuid references users(id),
  created_at timestamptz not null default now(),
  check (cost_amount >= 0),
  check (revenue_amount >= 0)
);
```

## Plans and entitlements

The v1 Stripe tables (`billing_customers`, `subscriptions`, `stripe_webhook_events`) are dropped (see [ADR 0007](../adr/0007-in-app-plan-management-no-external-billing.md)). Entitlements remain the single plan-gating source; changes go only through `app.set_org_plan` and are audited in `plan_changes`.

```sql
create table entitlements (
  organization_id uuid primary key references organizations(id) on delete cascade,
  plan plan_tier not null default 'free',
  max_fields int,
  csv_export_enabled boolean not null default false,
  season_report_enabled boolean not null default false,
  updated_by uuid references users(id),
  updated_at timestamptz not null default now(),
  check (max_fields is null or max_fields >= 0)
);

create table plan_changes (
  id uuid primary key default gen_random_uuid(),
  organization_id uuid not null references organizations(id) on delete cascade,
  from_plan plan_tier not null,
  to_plan plan_tier not null,
  changed_by uuid not null references users(id),
  changed_at timestamptz not null default now()
);
```

`authenticated` has no direct write grants on either table; see the `app.set_org_plan` `security definer` function in [[auth-rls]].

## Seeder and migration metadata

```sql
create table seed_runs (
  seed_key text primary key,
  completed_at timestamptz not null default now(),
  seed_version text not null
);

create table schema_migrations (
  filename text primary key,
  applied_at timestamptz not null default now()
);
```

`schema_migrations` tracks applied files from `db/migrations`; the migrate runner applies them in filename order under a pg advisory lock and records each here.

## Indexes

```sql
create index idx_org_members_user_org
  on organization_members (user_id, organization_id);

create index idx_fields_org_active
  on fields (organization_id)
  where archived_at is null;

create index idx_seasons_org_year
  on seasons (organization_id, year desc);

create index idx_field_seasons_org_season
  on field_seasons (organization_id, season_id);

create index idx_activities_org_field_season_date
  on activities (organization_id, field_id, season_id, activity_date desc);

create index idx_activities_org_season_type
  on activities (organization_id, season_id, activity_type);
```

## Migration ordering

1. Extensions (`pgcrypto`, `citext`).
2. Enum types.
3. `users`.
4. Organization and membership tables.
5. Farm operation tables.
6. Entitlement tables (`entitlements`, `plan_changes`).
7. Helper functions, including `app.set_org_plan`.
8. RLS enable/force statements.
9. Policies.
10. Triggers.
11. Grants.
12. Seed and migration marker tables (`seed_runs`, `schema_migrations`).

## Guardrail: tenant key consistency

Future migrations should add triggers or composite foreign keys to ensure `activities.organization_id`, `fields.organization_id`, and `seasons.organization_id` are consistent. This prevents accidentally linking an activity to a field from another organization even before RLS is evaluated.
