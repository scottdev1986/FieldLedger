-- 0006_auth_function_text_params.sql
-- The 0005 auth functions declared citext parameters. Typed client parameters
-- (e.g. Npgsql sending text) fail function resolution because text -> citext is an
-- ASSIGNMENT cast, not IMPLICIT. Recreate both functions with text parameters and
-- cast to citext inside, so any driver can call them without explicit casts.

drop function if exists app.get_user_for_login(citext);
drop function if exists app.register_user(citext, text, text);

create or replace function app.get_user_for_login(p_email text)
returns table (id uuid, email citext, display_name text, password_hash text)
language sql
stable
security definer
set search_path = public, app
as $$
  select u.id, u.email, u.display_name, u.password_hash
  from users u
  where u.email = p_email::citext;
$$;

create or replace function app.register_user(
  p_email text,
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
  values (p_email::citext, p_display_name, p_password_hash)
  returning * into v_user;

  return query
  select v_user.id, v_user.email, v_user.display_name, v_user.created_at;
end;
$$;

revoke all on function app.get_user_for_login(text) from public;
revoke all on function app.register_user(text, text, text) from public;

grant execute on function app.get_user_for_login(text) to fieldledger_api;
grant execute on function app.register_user(text, text, text) to fieldledger_api;
