---
title: Authentication and RLS
status: accepted
tags: [fieldledger, security, postgres, rls, aspnet, jwt]
last_updated: 2026-07-09
related: [architecture, database-schema, testing-ci]
---

# Authentication and RLS

This is the most important technical page in the design.

FieldLedger deliberately puts an ASP.NET Core API in front of PostgreSQL while preserving database-level RLS enforcement. Auth is first-party: the API owns email/password authentication and issues its own JWTs (see [ADR 0006](../adr/0006-self-contained-postgres-first-party-auth.md)). This showcases both C# backend work (token issuance, hashing, middleware) and Postgres authorization design.

## Security model

```text
Browser
  -> authenticates against the API (/api/auth/login)
  -> API verifies the PBKDF2 password hash and issues an HS256 JWT
  -> Browser sends the token as a Bearer header on API requests
  -> API validates its own token
  -> API opens DB transaction
  -> API sets transaction-local user context
  -> Postgres RLS uses that context to enforce tenant + role rules
```

See [auth-rls-sequence.mmd](../diagrams/auth-rls-sequence.mmd).

## First-party auth

The API is the identity provider. There is no external auth service.

Endpoints:

| Endpoint | Auth | Purpose |
|---|---|---|
| `POST /api/auth/register` | Anonymous | Email, password, display name. Creates a user (no org). |
| `POST /api/auth/login` | Anonymous | Returns `{ accessToken, user }`. |
| `GET /api/auth/me` | Bearer JWT | Current user profile + org memberships. |

Token issuance and validation:

- HS256 JWTs signed with `AUTH_JWT_SECRET` (>= 32 chars; dev default lives in `docker-compose.yml`).
- Issuer `AUTH_JWT_ISSUER=fieldledger-api`, audience `AUTH_JWT_AUDIENCE=fieldledger`.
- Lifetime `AUTH_TOKEN_LIFETIME_MINUTES` (default 720).
- Claims: `sub` (user id uuid), `email`, `name`.
- The API validates issuer, audience, signature, and expiry with standard ASP.NET Core JWT bearer middleware pointed at its own signing key.

Password storage:

- ASP.NET Core Identity `PasswordHasher` (PBKDF2). Hashes live in `users.password_hash`; plaintext passwords are never stored or logged.
- The seeder produces hashes with the same algorithm parameters so demo logins work against the API.

Auth data access (migration `0005_auth_access.sql`):

- Login and register run before any authenticated request context exists, so they cannot
  pass through the normal RLS path. They call security definer functions instead:
  `app.get_user_for_login(email)` (the only path that exposes `password_hash`, executable
  only by `fieldledger_api`) and `app.register_user(email, display_name, password_hash)`
  (email unique-violation maps to `409`).
- The members roster needs peer names, so `users` has a second select policy,
  `users_read_org_peers`, backed by security definer `app.shares_org_with(user_id)`
  (active shared org membership).
- The table-level `users` select grant is replaced by a column grant
  (`id, email, display_name, created_at`) so `authenticated` can never read
  `password_hash` directly.

Relevant docs:

- [ASP.NET Core JWT bearer authentication and PasswordHasher](../docs/source-map.md#aspnet-core)
- [PostgreSQL RLS](../docs/source-map.md#postgresql-and-rls)

## Request context forwarding

Because the API is issuing SQL queries, it must set the request context that RLS helpers expect.

For each request:

1. Authenticate and authorize at the ASP.NET Core middleware level.
2. Open an Npgsql connection.
3. Begin a transaction.
4. Set transaction-local role/context.
5. Execute application SQL.
6. Commit/rollback.
7. Dispose the transaction before returning the connection to the pool.

Conceptual SQL:

```sql
begin;

set local role authenticated;
select set_config('app.user_id', @user_id, true);

-- application SQL here

commit;
```

The config key is `app.user_id` (renamed from the PostgREST-compatible `request.jwt.claim.sub`; no PostgREST compatibility is needed anymore). `@user_id` is the validated JWT `sub` claim.

Use transaction-local settings only. Do not use session-level settings because pooled connections can leak state between requests.

## Database roles

Created by `db/init/01-roles.sql`, mounted into the postgres container's `docker-entrypoint-initdb.d`:

```sql
-- Group role targeted by RLS policies. No direct login.
create role authenticated nologin;

-- Normal API user traffic.
create role fieldledger_api login password '...';
grant authenticated to fieldledger_api;

-- Owns the app schema/tables; used by the migrate compose service and the seeder.
create role fieldledger_migrator login password '...';
```

Rules:

- `fieldledger_api` must not have `BYPASSRLS`.
- `fieldledger_migrator` connects via `DATABASE_ADMIN_URL` and is used only by the `migrate` service and the seeder — never by API or web user traffic.
- Dev-only passwords live in `docker-compose.yml` defaults; a production deployment would rotate them and split a separate system role. (`fieldledger_system` from v1 is dropped — without webhooks there is no unattended system write path.)

## Helper functions

```sql
create schema if not exists app;

create or replace function app.current_user_id()
returns uuid
language sql
stable
as $$
  select nullif(current_setting('app.user_id', true), '')::uuid;
$$;
```

```sql
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
```

```sql
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
```

## Plan changes: `app.set_org_plan`

Plan changes execute only through a `security definer` function (see [ADR 0007](../adr/0007-in-app-plan-management-no-external-billing.md)). `authenticated` has no direct write grants on `entitlements` or `plan_changes`; it only has `execute` on this function.

```sql
create or replace function app.set_org_plan(p_org_id uuid, p_plan plan_tier)
returns void
language plpgsql
security definer
set search_path = public, app
as $$
declare
  v_from plan_tier;
begin
  if not app.can_manage_org(p_org_id) then
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
```

## RLS enablement

```sql
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
```

## Example policies

### Organizations

```sql
create policy organizations_read_for_members
on organizations
for select
to authenticated
using (app.can_read_org(id));

create policy organizations_update_for_owners
on organizations
for update
to authenticated
using (app.can_manage_org(id))
with check (app.can_manage_org(id));
```

### Members

```sql
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
```

### Fields

```sql
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
```

### Activities

```sql
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
```

## RLS demo via psql

The demo runs against the local container. A superuser psql session bypasses RLS, so the README uses a role/claim simulation script:

```bash
docker compose exec db psql -U postgres -d fieldledger
```

Then paste the simulation script (placeholders replaced):

```sql
begin;

set local role authenticated;
select set_config('app.user_id', '<demo-user-uuid>', true);

select *
from fields
where organization_id = '<unauthorized-org-id>';

rollback;
```

Expected result: zero rows.

See [`templates/rls-demo.sql`](../templates/rls-demo.sql).

## Required tests

| Scenario | Expected result |
|---|---|
| Owner reads own org | Allowed |
| Owner reads unrelated org | Zero rows |
| Agronomist creates activity | Allowed |
| Non-owner calls `app.set_org_plan` | Exception raised |
| Viewer reads field | Allowed |
| Viewer creates activity | Denied |
| API forgets application-level check | RLS still denies unauthorized write |
| Free org attempts 4th active field | Denied by API and DB trigger |

## Security footguns to avoid

- Never connect user traffic as `fieldledger_migrator` (it owns the tables and bypasses RLS as owner).
- Never store or log plaintext passwords; only PBKDF2 hashes via `PasswordHasher`.
- Do not cache JWT claims as authorization state for membership unless token freshness is explicitly handled.
- Do not rely on UI role checks.
- Do not use session-level connection settings with pooled connections; use transaction-local settings only.
