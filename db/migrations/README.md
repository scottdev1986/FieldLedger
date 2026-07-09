# Database migrations

FieldLedger migrations are plain PostgreSQL 17 SQL files. The migrate runner scans this directory in filename order, acquires its advisory lock, creates/uses `schema_migrations`, and applies each unrecorded file in its own transaction. The runner records the filename only after the SQL succeeds.

Migration files therefore do not contain `BEGIN`, `COMMIT`, or a `schema_migrations` definition. A failed file rolls back as a unit and remains pending for the next run.

## Ordering

1. `0001_extensions_and_types.sql` installs `pgcrypto` and `citext`, then creates the v2 enum types.
2. `0002_tables_and_indexes.sql` creates users, tenant data, plans, seed metadata, constraints, and indexes.
3. `0003_app_functions_and_triggers.sql` creates the `app` helpers, controlled organization/plan functions, creator bootstrap, and field-limit guardrail.
4. `0004_rls_policies_and_grants.sql` forces RLS, creates policies, and grants the runtime role only the intended privileges.
5. `0005_auth_access.sql` adds the pre-auth login/register security definer functions, the org-peer visibility policy, and the column grant that hides `password_hash`.

## Apply locally

The `db` service runs `db/init/01-roles.sql` only when its named volume is first initialized. The one-shot `migrate` service then mounts this directory read-only at `/db/migrations` and runs the seeder image with `command: migrate` using `DATABASE_ADMIN_URL`.

```bash
docker compose up --build db migrate
```

For the normal stack, `docker compose up --build` waits for the database healthcheck, requires `migrate` to exit successfully, and only then starts the API. If role bootstrap changes during local development, recreate the disposable local database with `docker compose down -v` before starting it again.

Do not apply these files manually and then run the migration service unless the runner's `schema_migrations` records are also correct; the runner is the source of truth for applied filenames.
