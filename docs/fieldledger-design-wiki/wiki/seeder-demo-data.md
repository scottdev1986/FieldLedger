---
title: Seeder and Demo Data
status: accepted
tags: [fieldledger, seeding, demo-data, csharp]
last_updated: 2026-07-09
related: [domain-model, docker-local-dev, demo-script]
---

# Seeder and Demo Data

The seeder is a dedicated C# console app run through Docker Compose.

```bash
docker compose run --rm seeder
```

Its job is to make the demo credible from zero.

The seeder connects with `DATABASE_ADMIN_URL` (the `fieldledger_migrator` role) directly via Npgsql; it never uses the API's connection.

## Responsibilities

1. Create demo users with direct Npgsql inserts into the `users` table, using PBKDF2 password hashes compatible with the API's `PasswordHasher` (share the hashing code or algorithm parameters so seeded logins work against the API).
2. Create two organizations.
3. Assign owner/agronomist/viewer memberships.
4. Generate three seasons of realistic field data.
5. Generate deterministic activities, costs, yields, and harvest values.
6. Be safe to rerun.

## Migrate mode

The same binary exposes a `migrate` command, used by the one-shot `migrate` service in Docker Compose. It applies `db/migrations/*.sql` in filename order under a pg advisory lock, recording each applied file as a row in `schema_migrations (filename, applied_at)`. Already-recorded files are skipped, so the service is safe to run on every `docker compose up`.

## Demo users

| Email | Password | Role |
|---|---|---|
| `owner@fieldledger.demo` | `FieldLedgerDemo!2026` | Owner |
| `agronomist@fieldledger.demo` | `FieldLedgerDemo!2026` | Agronomist |
| `viewer@fieldledger.demo` | `FieldLedgerDemo!2026` | Viewer |

Do not use these credentials outside local/demo contexts.

## Demo organizations

| Organization | Slug | Purpose |
|---|---|---|
| North Fork Farms | `north-fork-farms` | Primary demo org with Free-to-Pro upgrade flow. |
| Prairie View Ag Co | `prairie-view-ag-co` | Second org for org switching and cross-tenant RLS demo. |

The owner belongs to both organizations. The agronomist and viewer can belong to `North Fork Farms` initially to make role switching obvious.

## Seeded fields

Example field set:

| Field | Acreage | Default crop |
|---|---:|---|
| River Bottom 40 | 40.0 | Corn |
| West Ridge | 62.5 | Soybean |
| South Pivot | 118.0 | Corn |
| Home Place Wheat | 35.0 | Wheat |

North Fork starts on Free with three active fields; the fourth field is seeded archived so the Free field-limit demo is clean. After the owner runs the in-app Pro upgrade, they unarchive the fourth field to show the limit lifting.

## Deterministic randomness

Use deterministic seeds derived from stable strings:

```text
fieldledger-demo-v1:{orgSlug}:{fieldName}:{crop}:{seasonYear}
```

This keeps numbers realistic but repeatable.

## Crop calendars

| Crop | Planting window | Spray window | Harvest window |
|---|---|---|---|
| Corn | April–May | May–July | September–November |
| Soybean | May–June | June–August | September–October |
| Wheat | September–October previous year or early spring variant | April–May | June–July |

## Activity generation

For each field/season:

1. Planting activity.
2. Optional fertilizer pass.
3. One to three spray passes.
4. Zero to four irrigation entries depending on crop/field.
5. Harvest activity.
6. Optional note activity.

## Value generation

Example ranges for seeded realism:

| Crop | Yield unit | Yield range | Harvest value driver |
|---|---|---:|---|
| Corn | bushels/acre | 150–230 | bushels × price/bushel |
| Soybean | bushels/acre | 40–75 | bushels × price/bushel |
| Wheat | bushels/acre | 55–100 | bushels × price/bushel |

Costs:

- seed cost,
- fertilizer cost,
- chemical/spray pass cost,
- irrigation cost,
- harvest/logistics cost.

## Idempotency approach

Use a `seed_runs` marker plus upserts.

```sql
select 1 from seed_runs where seed_key = 'fieldledger-demo-v1';
```

Seeder behavior:

1. If marker exists and `--force` is not provided, exit successfully.
2. Upsert users by email.
3. Upsert organizations by slug.
4. Upsert memberships by `(organization_id, user_id)`.
5. Upsert fields by `(organization_id, name)`.
6. Upsert seasons by `(organization_id, year)`.
7. Delete/recreate seeded activities in one transaction, or store deterministic external keys.
8. Insert marker only after success.

## Seeder CLI options

```bash
docker compose run --rm seeder --help

docker compose run --rm seeder --seed-version fieldledger-demo-v1

docker compose run --rm seeder --force
```

## README demo credentials section

The README should include a prominent development-only block:

```md
## Demo logins

Owner: owner@fieldledger.demo / FieldLedgerDemo!2026  
Agronomist: agronomist@fieldledger.demo / FieldLedgerDemo!2026  
Viewer: viewer@fieldledger.demo / FieldLedgerDemo!2026
```
