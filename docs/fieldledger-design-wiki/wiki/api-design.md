---
title: API Design
status: proposed
tags: [fieldledger, api, aspnet]
last_updated: 2026-07-09
related: [architecture, auth-rls, billing-entitlements]
---

# API Design

The ASP.NET Core API owns first-party authentication, issues and validates its own JWTs, forwards request context into Postgres, exposes business endpoints, manages plans and entitlements, and generates reports/exports.

## API principles

1. Every application endpoint requires authentication except health checks and `POST /api/auth/register` / `POST /api/auth/login`.
2. Every tenant-scoped endpoint includes `orgId` in the path.
3. Handlers check application-level role/entitlement rules for clear errors.
4. Database RLS remains the final authorization boundary.
5. The API never trusts browser-supplied plan state.
6. The API never exposes migrator credentials.

## Middleware pipeline

```text
Request
  -> exception handling
  -> request logging / correlation id
  -> authentication
  -> authorization
  -> tenant context middleware
  -> controller/minimal API handler
  -> DB transaction with RLS context
  -> response
```

## Error shape

```json
{
  "error": {
    "code": "field_limit_reached",
    "message": "Free organizations can have up to 3 active fields.",
    "traceId": "00-..."
  }
}
```

Suggested status codes:

| Status | Use |
|---:|---|
| 400 | Validation error. |
| 401 | Missing or invalid bearer token. |
| 403 | Authenticated but not allowed. |
| 404 | Resource not visible to caller or does not exist. |
| 409 | Idempotency conflict or duplicate natural key. |
| 422 | Business rule failure, such as field limit. |
| 500 | Unexpected server error. |

## Auth endpoints

```http
POST /api/auth/register
POST /api/auth/login
GET  /api/auth/me
```

Rules:

- `register` and `login` are anonymous; `me` requires a valid bearer token.
- `register` takes email, password, and display name and creates a user (no org).
- `login` returns `{ accessToken, user }`; the token is an API-issued HS256 JWT (see [[auth-rls]]).
- `me` returns the current user profile plus org memberships.

## Organization endpoints

```http
GET    /api/orgs
POST   /api/orgs
GET    /api/orgs/{orgId}
GET    /api/orgs/{orgId}/dashboard
PATCH  /api/orgs/{orgId}
```

## Member endpoints

```http
GET    /api/orgs/{orgId}/members
POST   /api/orgs/{orgId}/members
PATCH  /api/orgs/{orgId}/members/{userId}
DELETE /api/orgs/{orgId}/members/{userId}
```

Rules:

- Read: active organization members.
- Write: owners only.
- Prevent removing the last owner.

## Field endpoints

```http
GET    /api/orgs/{orgId}/fields
POST   /api/orgs/{orgId}/fields
GET    /api/orgs/{orgId}/fields/{fieldId}
PATCH  /api/orgs/{orgId}/fields/{fieldId}
DELETE /api/orgs/{orgId}/fields/{fieldId}
```

Rules:

- Read: active members.
- Write: owner/agronomist.
- Create must check Free field limit.
- Delete should archive instead of hard-delete in v1.

## Season endpoints

```http
GET    /api/orgs/{orgId}/seasons
POST   /api/orgs/{orgId}/seasons
GET    /api/orgs/{orgId}/seasons/{seasonId}
PATCH  /api/orgs/{orgId}/seasons/{seasonId}
```

Rules:

- One season per year per organization.
- Owner/agronomist can create and edit.

## Activity endpoints

```http
GET    /api/orgs/{orgId}/fields/{fieldId}/activities
POST   /api/orgs/{orgId}/fields/{fieldId}/activities
PATCH  /api/orgs/{orgId}/activities/{activityId}
DELETE /api/orgs/{orgId}/activities/{activityId}
```

Rules:

- Read: active members.
- Write: owner/agronomist.
- Activity `organization_id`, `field_id`, and `season_id` must be consistent.

## Insights and reports

```http
GET /api/orgs/{orgId}/insights
GET /api/orgs/{orgId}/seasons/{seasonId}/report
GET /api/orgs/{orgId}/seasons/{seasonId}/report.pdf
GET /api/orgs/{orgId}/exports/activities.csv
```

Rules:

- Insights: active members.
- CSV export: Pro and owner/agronomist.
- Season report: Pro and active member.
- PDF generation is server-side.

## Plan endpoints (billing route shape)

Plan changes are in-app and owner-only; no payment provider is involved (see [ADR 0007](../adr/0007-in-app-plan-management-no-external-billing.md)). The `/billing` route shape is kept for realism.

```http
GET  /api/orgs/{orgId}/billing
POST /api/orgs/{orgId}/billing/upgrade
POST /api/orgs/{orgId}/billing/downgrade
```

Rules:

- Read: active members may see plan, limits, usage (active field count), and recent `plan_changes` history.
- Upgrade/downgrade: owner only (403 otherwise). Both run in the user transaction and call `app.set_org_plan(orgId, plan)`; the response includes the new plan state so the web app can refetch immediately.
- Downgrade returns 422 `too_many_active_fields_for_free` when the org has more than 3 active fields; the owner must archive fields first.

## Request-context abstraction

Recommended internal API shape:

```csharp
public interface IFieldLedgerDbSession
{
    Task<T> InUserTransaction<T>(ClaimsPrincipal user, Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> work);
}
```

The implementation owns claim forwarding and `SET LOCAL` behavior so individual endpoint handlers cannot forget it. `InUserTransaction` is the only path: the API always connects as `fieldledger_api` under RLS. Elevated work (migrations, seeding) runs out-of-band as `fieldledger_migrator` via the `migrate`/`seeder` compose services, never through the API.

See [`templates/api-claim-forwarding.example.cs`](../templates/api-claim-forwarding.example.cs).
