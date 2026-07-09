-- 0005_auth_access.sql
-- First-party auth data access.
--
-- Login and register run before an authenticated request context exists, and the
-- members roster must show display names of org peers. All three paths go through
-- security definer functions (owned by fieldledger_migrator, which bypasses RLS) or a
-- narrowly scoped policy, so the `authenticated` role never gains direct access to
-- users.password_hash.

-- Pre-auth lookup used by POST /api/auth/login. The only path that exposes
-- password_hash, and only to the API login handler.
create or replace function app.get_user_for_login(p_email citext)
returns table (id uuid, email citext, display_name text, password_hash text)
language sql
stable
security definer
set search_path = public, app
as $$
  select u.id, u.email, u.display_name, u.password_hash
  from users u
  where u.email = p_email;
$$;

-- Pre-auth insert used by POST /api/auth/register. Unique-violation on email
-- propagates to the caller (API maps it to 409).
create or replace function app.register_user(
  p_email citext,
  p_display_name text,
  p_password_hash text
)
returns table (id uuid, email citext, display_name text, created_at timestamptz)
language plpgsql
security definer
set search_path = public, app
as $$
declare
  v_user users%rowtype;
begin
  insert into users (email, display_name, password_hash)
  values (p_email, p_display_name, p_password_hash)
  returning * into v_user;

  return query
  select v_user.id, v_user.email, v_user.display_name, v_user.created_at;
end;
$$;

-- True when the current user shares at least one active org membership with the
-- given user. Security definer so the users policy below does not recurse through
-- organization_members RLS.
create or replace function app.shares_org_with(p_user_id uuid)
returns boolean
language sql
stable
security definer
set search_path = public, app
as $$
  select exists (
    select 1
    from organization_members mine
    join organization_members theirs
      on theirs.organization_id = mine.organization_id
    where mine.user_id = app.current_user_id()
      and mine.status = 'active'
      and theirs.user_id = p_user_id
      and theirs.status = 'active'
  );
$$;

-- Org peers may see each other's rows (columns limited by the grant below).
create policy users_read_org_peers
on users
for select
to authenticated
using (app.shares_org_with(id));

-- Tighten users access: replace the table-level select grant from 0004 with a
-- column list that excludes password_hash.
revoke select on users from authenticated;
grant select (id, email, display_name, created_at) on users to authenticated;

revoke all on function app.get_user_for_login(citext) from public;
revoke all on function app.register_user(citext, text, text) from public;
revoke all on function app.shares_org_with(uuid) from public;

-- Pre-auth functions are for the API service role only; peer check is for RLS use.
grant execute on function app.get_user_for_login(citext) to fieldledger_api;
grant execute on function app.register_user(citext, text, text) to fieldledger_api;
grant execute on function app.shares_org_with(uuid) to authenticated;
