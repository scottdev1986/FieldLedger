alter table users enable row level security;
alter table users force row level security;

alter table organizations enable row level security;
alter table organizations force row level security;

alter table organization_members enable row level security;
alter table organization_members force row level security;

alter table fields enable row level security;
alter table fields force row level security;

alter table seasons enable row level security;
alter table seasons force row level security;

alter table field_seasons enable row level security;
alter table field_seasons force row level security;

alter table activities enable row level security;
alter table activities force row level security;

alter table entitlements enable row level security;
alter table entitlements force row level security;

alter table plan_changes enable row level security;
alter table plan_changes force row level security;

create policy users_read_own_row
on users
for select
to authenticated
using (id = app.current_user_id());

create policy organizations_read_for_members
on organizations
for select
to authenticated
using (app.can_read_org(id));

create policy organizations_insert_by_creator
on organizations
for insert
to authenticated
with check (created_by = app.current_user_id());

create policy organizations_update_for_owners
on organizations
for update
to authenticated
using (app.can_manage_org(id))
with check (app.can_manage_org(id));

create policy members_read_for_org_members
on organization_members
for select
to authenticated
using (app.can_read_org(organization_id));

create policy members_manage_for_owners
on organization_members
for all
to authenticated
using (app.can_manage_org(organization_id))
with check (app.can_manage_org(organization_id));

create policy fields_read_for_org_members
on fields
for select
to authenticated
using (app.can_read_org(organization_id));

create policy fields_write_for_owners_and_agronomists
on fields
for all
to authenticated
using (app.can_edit_org(organization_id))
with check (app.can_edit_org(organization_id));

create policy seasons_read_for_org_members
on seasons
for select
to authenticated
using (app.can_read_org(organization_id));

create policy seasons_write_for_owners_and_agronomists
on seasons
for all
to authenticated
using (app.can_edit_org(organization_id))
with check (app.can_edit_org(organization_id));

create policy field_seasons_read_for_org_members
on field_seasons
for select
to authenticated
using (app.can_read_org(organization_id));

create policy field_seasons_write_for_owners_and_agronomists
on field_seasons
for all
to authenticated
using (app.can_edit_org(organization_id))
with check (app.can_edit_org(organization_id));

create policy activities_read_for_org_members
on activities
for select
to authenticated
using (app.can_read_org(organization_id));

create policy activities_write_for_owners_and_agronomists
on activities
for all
to authenticated
using (app.can_edit_org(organization_id))
with check (app.can_edit_org(organization_id));

create policy entitlements_read_for_org_members
on entitlements
for select
to authenticated
using (app.can_read_org(organization_id));

create policy plan_changes_read_for_org_members
on plan_changes
for select
to authenticated
using (app.can_read_org(organization_id));

grant usage on schema public, app to authenticated;
grant usage on type member_role, plan_tier, activity_type, crop_type to authenticated;

grant select on users to authenticated;
grant select, insert, update on organizations to authenticated;
grant select, insert, update, delete on organization_members to authenticated;
grant select, insert, update, delete on fields to authenticated;
grant select, insert, update, delete on seasons to authenticated;
grant select, insert, update, delete on field_seasons to authenticated;
grant select, insert, update, delete on activities to authenticated;

revoke insert, update, delete on entitlements from authenticated;
revoke insert, update, delete on plan_changes from authenticated;
grant select on entitlements, plan_changes to authenticated;

revoke all on function app.current_user_id() from public;
revoke all on function app.member_role(uuid) from public;
revoke all on function app.can_read_org(uuid) from public;
revoke all on function app.can_edit_org(uuid) from public;
revoke all on function app.can_manage_org(uuid) from public;
revoke all on function app.create_organization(text, text) from public;
revoke all on function app.set_org_plan(uuid, plan_tier) from public;

grant execute on function app.current_user_id() to authenticated;
grant execute on function app.member_role(uuid) to authenticated;
grant execute on function app.can_read_org(uuid) to authenticated;
grant execute on function app.can_edit_org(uuid) to authenticated;
grant execute on function app.can_manage_org(uuid) to authenticated;
grant execute on function app.create_organization(text, text) to authenticated;
grant execute on function app.set_org_plan(uuid, plan_tier) to authenticated;
