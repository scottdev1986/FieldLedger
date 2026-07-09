-- FieldLedger initial migration skeleton (v2 self-contained stack)
-- Applied from db/migrations/*.sql in filename order by the seeder binary in
-- `migrate` mode, under a pg advisory lock, recording each file in schema_migrations.

begin;

-- 1. Extensions
create extension if not exists pgcrypto;
create extension if not exists citext;

-- 2. Enum types
-- create type member_role ...
-- create type plan_tier ...
-- create type activity_type ...
-- create type crop_type ...

-- 3. Tables
-- users (first-party identity: email citext unique, display_name, password_hash)
-- organizations
-- organization_members
-- fields
-- seasons
-- field_seasons
-- activities
-- entitlements (plan gating; updated_by references users)
-- plan_changes (audit of plan transitions)
-- seed_runs
-- schema_migrations

-- 4. Indexes

-- 5. App schema and helper functions
create schema if not exists app;

-- app.current_user_id()  -- reads current_setting('app.user_id', true)
-- app.member_role(org_id)
-- app.can_read_org(org_id)
-- app.can_edit_org(org_id)
-- app.can_manage_org(org_id)
-- app.set_org_plan(org_id, plan)  -- security definer; owner check + plan_changes audit row

-- 6. Enable and force RLS
-- alter table ... enable row level security;
-- alter table ... force row level security;

-- 7. Policies

-- 8. Triggers / guardrails
-- Free-plan field limit trigger
-- Tenant key consistency trigger

-- 9. Grants
-- grant select/insert/update on tenant tables to authenticated (per policy design)
-- no direct write grants on entitlements/plan_changes to authenticated;
-- grant execute on app.set_org_plan to authenticated

commit;
