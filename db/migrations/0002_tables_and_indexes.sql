-- Core v2 tables. The migration runner creates and uses schema_migrations;
-- migrations must not create or modify that runner-owned table.

create table users (
  id uuid primary key default gen_random_uuid(),
  email citext not null unique,
  display_name text not null,
  password_hash text not null,
  created_at timestamptz not null default now()
);

create table organizations (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  slug text not null unique,
  created_by uuid not null references users(id),
  created_at timestamptz not null default now(),
  archived_at timestamptz
);

create table organization_members (
  organization_id uuid not null references organizations(id) on delete cascade,
  user_id uuid not null references users(id) on delete cascade,
  role member_role not null,
  status text not null default 'active',
  created_at timestamptz not null default now(),
  primary key (organization_id, user_id)
);

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

create table seed_runs (
  seed_key text primary key,
  completed_at timestamptz not null default now(),
  seed_version text not null
);

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
