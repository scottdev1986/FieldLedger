---
title: Docker and Local Development
status: proposed
tags: [fieldledger, docker, local-dev]
last_updated: 2026-07-09
related: [architecture, seeder-demo-data, billing-entitlements]
---

# Docker and Local Development

FieldLedger is designed to demo from zero with Docker Compose alone. Everything — database, migrations, API, web, seeding — runs in local containers.

## Services

| Service | Purpose |
|---|---|
| `db` | PostgreSQL 17 container (`postgres:17-alpine`) with RLS-enforced schema. |
| `migrate` | One-shot run of the seeder binary in `migrate` mode; applies `db/migrations/*.sql`. |
| `api` | ASP.NET Core Web API; starts after `migrate` completes successfully. |
| `web` | Next.js/React/Tailwind frontend. |
| `seeder` | C# console app that creates demo users/data (profile `tools`). |

## External dependencies

None. No hosted database, no identity provider, no payment provider, no webhook forwarder. The only requirement is a local Docker runtime. See [ADR 0006](../adr/0006-self-contained-postgres-first-party-auth.md) and [ADR 0007](../adr/0007-in-app-plan-management-no-external-billing.md).

## Compose skeleton

See [`templates/docker-compose.example.yml`](../templates/docker-compose.example.yml).

```yaml
services:
  db:
    image: postgres:17-alpine
    environment:
      POSTGRES_DB: fieldledger
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-fieldledger-postgres-dev}
    volumes:
      - dbdata:/var/lib/postgresql/data
      - ./db/init:/docker-entrypoint-initdb.d
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d fieldledger"]
      interval: 2s
      timeout: 3s
      retries: 15

  migrate:
    build:
      context: ./apps/seeder
    command: migrate
    env_file:
      - .env
    depends_on:
      db:
        condition: service_healthy

  api:
    build:
      context: ./apps/api
    ports:
      - "8080:8080"
    env_file:
      - .env
    depends_on:
      migrate:
        condition: service_completed_successfully

  web:
    build:
      context: ./apps/web
    ports:
      - "3000:3000"
    env_file:
      - .env
    depends_on:
      - api

  seeder:
    build:
      context: ./apps/seeder
    env_file:
      - .env
    profiles:
      - tools

volumes:
  dbdata:
```

## Environment variables

See [`templates/.env.example`](../templates/.env.example). Every variable has a working local default, so `.env` is optional. Dev-only passwords/secrets below are local defaults, not real credentials.

### Web

```bash
NEXT_PUBLIC_API_BASE_URL=http://localhost:8080
```

### API

```bash
ASPNETCORE_ENVIRONMENT=Development
DATABASE_URL=Host=db;Port=5432;Database=fieldledger;Username=fieldledger_api;Password=fieldledger_api_dev
AUTH_JWT_SECRET=fieldledger-local-dev-secret-change-me-0123456789
AUTH_JWT_ISSUER=fieldledger-api
AUTH_JWT_AUDIENCE=fieldledger
AUTH_TOKEN_LIFETIME_MINUTES=720
APP_PUBLIC_URL=http://localhost:3000
```

`AUTH_JWT_SECRET` must be at least 32 characters (HS256 signing key).

### Postgres container

```bash
POSTGRES_PASSWORD=fieldledger-postgres-dev
```

### Migrate + seeder (elevated; never used by web/api user traffic)

```bash
DATABASE_ADMIN_URL=Host=db;Port=5432;Database=fieldledger;Username=fieldledger_migrator;Password=fieldledger_migrator_dev
SEED_VERSION=fieldledger-demo-v1
```

## Local setup flow

```bash
cp .env.example .env   # optional — the defaults work as-is
docker compose up --build
```

Compose ordering does the rest: `db` starts and passes its healthcheck, `migrate` applies migrations and exits, then `api` and `web` start.

Then seed:

```bash
docker compose run --rm seeder
```

## Database role bootstrap (`db/init`)

On first start the postgres container runs `db/init/01-roles.sql` (mounted into `/docker-entrypoint-initdb.d`), which creates:

- `authenticated` — NOLOGIN group role; RLS policies target it.
- `fieldledger_api` — LOGIN, member of `authenticated`, no `BYPASSRLS`; the API's connection role.
- `fieldledger_migrator` — LOGIN, owns the app schema/tables; used by `migrate` and the seeder via `DATABASE_ADMIN_URL`.

Because init scripts only run against an empty data volume, changing roles later requires `docker compose down -v` (or a manual `psql` change).

## RLS demo note

The RLS walkthrough runs directly against the local container:

```bash
docker compose exec db psql -U postgres -d fieldledger
```

Use the simulation script from [`templates/rls-demo.sql`](../templates/rls-demo.sql): `set local role authenticated` plus `select set_config('app.user_id', '<uuid>', true)` inside a transaction, then query tenant tables. See [[auth-rls]].

Relevant docs:

- [PostgreSQL and RLS](../docs/source-map.md#postgresql-and-rls)
- [Docker Compose environment variables](../docs/source-map.md#docker)

## Local dev gotchas

| Gotcha | Mitigation |
|---|---|
| `migrate` service failed | Check the `db` healthcheck passed and `depends_on` ordering; inspect `docker compose logs migrate`. |
| RLS returns zero rows unexpectedly | Confirm API set transaction-local role and `app.user_id` config. |
| RLS appears bypassed in psql | You are superuser/table owner; use the simulation script with `set local role authenticated`. |
| Role changes in `db/init` not applied | Init scripts only run on an empty volume; `docker compose down -v` and re-up. |
| Frontend shows stale plan | Refetch plan state after the upgrade/downgrade mutation resolves. |
| Seeder duplicates data | Use `seed_runs`, upserts, and deterministic keys. |
