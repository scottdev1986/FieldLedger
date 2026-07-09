-- FieldLedger RLS simulation script
-- Run inside the local database container to demonstrate RLS from a normal
-- authenticated user context:
--
--   docker compose exec db psql -U postgres -d fieldledger
--
-- then paste this script. Replace placeholders before running.

begin;

-- Simulate the runtime role used by authenticated user traffic.
set local role authenticated;

-- Simulate the API's transaction-local user context (the validated JWT `sub` claim).
select set_config('app.user_id', '<DEMO_USER_UUID>', true);

-- Should return rows only for organizations where the user is an active member.
select id, name, slug
from organizations
order by name;

-- Should return zero rows when organization id is not visible to this user.
select id, organization_id, name, acreage
from fields
where organization_id = '<UNAUTHORIZED_ORG_UUID>';

rollback;
