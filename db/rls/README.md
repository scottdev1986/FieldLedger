# RLS checks

`rls-checks.sql` is an executable psql assertion script for PostgreSQL 17. It creates two users and two organizations inside one transaction, switches to the `authenticated` runtime role, and asserts tenant visibility, viewer write denial, owner-only plan changes, entitlement write protection, and the Free-plan three-field limit.

Every fixture is rolled back. An unexpected result raises an exception, and `ON_ERROR_STOP` makes psql return a non-zero exit status for local and CI use.

## Run locally

Start and migrate the database, then pipe the script into psql as the local postgres superuser. A superuser is used only to create the throwaway fixtures and to run `SET LOCAL ROLE authenticated`; the assertions themselves execute as `authenticated` with `app.user_id` set transaction-locally.

```bash
docker compose up -d db
docker compose run --rm migrate
docker compose exec -T db psql -U postgres -d fieldledger < db/rls/rls-checks.sql
```

A successful run ends with `ROLLBACK`. In CI, invoke the same file with `psql -v ON_ERROR_STOP=1 -f db/rls/rls-checks.sql` using a PostgreSQL role that can seed fixtures and `SET ROLE authenticated`.
