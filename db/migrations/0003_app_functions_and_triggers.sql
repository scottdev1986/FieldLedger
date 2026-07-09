create schema if not exists app;

create or replace function app.current_user_id()
returns uuid
language sql
stable
as $$
  select nullif(current_setting('app.user_id', true), '')::uuid;
$$;

create or replace function app.member_role(p_org_id uuid)
returns member_role
language sql
stable
security definer
set search_path = public, app
as $$
  select om.role
  from organization_members om
  where om.organization_id = p_org_id
    and om.user_id = app.current_user_id()
    and om.status = 'active'
  limit 1;
$$;

create or replace function app.can_read_org(p_org_id uuid)
returns boolean
language sql
stable
security definer
set search_path = public, app
as $$
  select app.member_role(p_org_id) is not null;
$$;

create or replace function app.can_edit_org(p_org_id uuid)
returns boolean
language sql
stable
security definer
set search_path = public, app
as $$
  select app.member_role(p_org_id) in ('owner', 'agronomist');
$$;

create or replace function app.can_manage_org(p_org_id uuid)
returns boolean
language sql
stable
security definer
set search_path = public, app
as $$
  select app.member_role(p_org_id) = 'owner';
$$;

create or replace function app.bootstrap_new_organization()
returns trigger
language plpgsql
security definer
set search_path = public, app
as $$
begin
  insert into organization_members (organization_id, user_id, role)
  values (new.id, new.created_by, 'owner')
  on conflict (organization_id, user_id) do nothing;

  insert into entitlements (
    organization_id,
    plan,
    max_fields,
    csv_export_enabled,
    season_report_enabled,
    updated_by
  )
  values (new.id, 'free', 3, false, false, new.created_by)
  on conflict (organization_id) do nothing;

  return new;
end;
$$;

create trigger organizations_bootstrap_creator
after insert on organizations
for each row
execute function app.bootstrap_new_organization();

create or replace function app.create_organization(p_name text, p_slug text)
returns organizations
language plpgsql
security definer
set search_path = public, app
as $$
declare
  v_organization organizations%rowtype;
begin
  if app.current_user_id() is null then
    raise exception 'authentication context required';
  end if;

  insert into organizations (name, slug, created_by)
  values (p_name, p_slug, app.current_user_id())
  returning * into v_organization;

  -- The organizations_bootstrap_creator trigger creates the owner membership
  -- and default Free entitlement in the same transaction.
  return v_organization;
end;
$$;

create or replace function app.set_org_plan(p_org_id uuid, p_plan plan_tier)
returns void
language plpgsql
security definer
set search_path = public, app
as $$
declare
  v_from plan_tier;
begin
  if app.can_manage_org(p_org_id) is not true then
    raise exception 'only an organization owner can change the plan';
  end if;

  select plan into v_from
  from entitlements
  where organization_id = p_org_id
  for update;

  update entitlements
  set plan = p_plan,
      max_fields = case when p_plan = 'pro' then null else 3 end,
      csv_export_enabled = (p_plan = 'pro'),
      season_report_enabled = (p_plan = 'pro'),
      updated_by = app.current_user_id(),
      updated_at = now()
  where organization_id = p_org_id;

  insert into plan_changes (organization_id, from_plan, to_plan, changed_by)
  values (p_org_id, v_from, p_plan, app.current_user_id());
end;
$$;

create or replace function app.enforce_field_limit()
returns trigger
language plpgsql
security definer
set search_path = public, app
as $$
declare
  v_max_fields int;
  v_resulting_active_fields bigint;
begin
  if new.archived_at is not null then
    return new;
  end if;

  -- Lock the entitlement row so concurrent active-field writes for one
  -- organization are serialized before the count is evaluated.
  select e.max_fields
  into v_max_fields
  from entitlements e
  where e.organization_id = new.organization_id
  for update;

  if v_max_fields is null then
    return new;
  end if;

  select count(*)
  into v_resulting_active_fields
  from fields f
  where f.organization_id = new.organization_id
    and f.archived_at is null;

  if tg_op = 'INSERT'
     or old.archived_at is not null
     or old.organization_id <> new.organization_id then
    v_resulting_active_fields := v_resulting_active_fields + 1;
  end if;

  if v_resulting_active_fields > v_max_fields then
    raise exception 'field_limit_reached';
  end if;

  return new;
end;
$$;

create trigger fields_enforce_plan_limit
before insert or update on fields
for each row
execute function app.enforce_field_limit();

revoke all on function app.bootstrap_new_organization() from public;
revoke all on function app.enforce_field_limit() from public;
