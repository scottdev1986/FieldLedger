# FieldLedger Web API Contract

This is the implementation contract between `apps/web` and the FieldLedger API. All property names are camelCase. Dates are ISO `YYYY-MM-DD` strings; timestamps are ISO 8601 UTC strings; IDs are UUID strings; money and other decimal values are JSON numbers. Request and response bodies use `application/json` except the CSV download. Unknown response properties may be ignored, but the properties documented here are required unless their type includes `null`.

## Shared values and errors

- `Role`: `"owner" | "agronomist" | "viewer"`
- `Plan`: `"free" | "pro"`
- `ActivityType`: `"planting" | "spraying" | "irrigation" | "fertilizer" | "harvest" | "note"`
- `FieldStatus`: `"active" | "archived"`
- Authenticated endpoints require `Authorization: Bearer <accessToken>`.

Every non-2xx JSON response has this exact envelope. `fieldErrors` is present only for field-level validation errors.

```json
{
  "error": {
    "code": "field_limit_reached",
    "message": "Free organizations can have up to 3 active fields.",
    "traceId": "00-correlation-id",
    "fieldErrors": { "name": ["A field with this name already exists."] }
  }
}
```

Common codes: `validation_error` (400), `unauthorized` (401), `forbidden` (403), `not_found` (404), `conflict` (409), and `internal_error` (500). A 401 causes the web app to clear its session and redirect to `/login`. Tenant-scoped 404 responses deliberately do not reveal whether an inaccessible resource exists.

Shared objects used below:

```ts
type User = { id: string; email: string; displayName: string; createdAt: string };
type Season = { id: string; year: number; name: string; startsOn: string; endsOn: string };
type Organization = {
  id: string; name: string; slug: string; role: Role; plan: Plan;
  activeFieldCount: number; seasonCount: number; currentSeason: Season | null;
};
type Activity = {
  id: string; organizationId: string; fieldId: string; fieldName: string;
  fieldAcreage: number; seasonId: string; seasonName: string; type: ActivityType;
  activityDate: string; quantity: number | null; quantityUnit: string | null;
  costAmount: number | null; revenueAmount: number | null; notes: string | null;
  createdBy: { id: string; displayName: string }; createdAt: string;
};
type Field = {
  id: string; name: string; acreage: number; defaultCrop: string;
  soilType: string | null; status: FieldStatus; currentCrop: string | null;
  lastActivity: { type: ActivityType; activityDate: string } | null;
};
```

## Authentication

### `POST /api/auth/login`

Auth: anonymous.

Request:

```json
{ "email": "owner@fieldledger.demo", "password": "FieldLedgerDemo!2026" }
```

Response `200`:

```ts
{ accessToken: string; user: User }
```

Handled errors: `validation_error` (400), `invalid_credentials` (401), `internal_error` (500).

### `POST /api/auth/register`

Auth: anonymous. Registration creates the user only; it does not create an organization.

Request:

```json
{ "email": "farmer@example.com", "displayName": "Alex Farmer", "password": "long-password" }
```

Response `201`:

```ts
{ accessToken: string; user: User }
```

Handled errors: `validation_error` (400), `email_already_registered` (409), `internal_error` (500).

### `GET /api/auth/me`

Auth: bearer.

Response `200`:

```ts
{
  user: User;
  memberships: Array<{
    organizationId: string; organizationName: string; organizationSlug: string;
    role: Role; plan: Plan;
  }>;
}
```

Handled errors: `unauthorized` (401), `internal_error` (500).

## Organizations and dashboard

### `GET /api/orgs`

Auth: bearer. Returns only organizations where the caller is an active member.

Response `200`:

```ts
{ organizations: Organization[] }
```

Handled errors: `unauthorized` (401), `internal_error` (500).

### `POST /api/orgs`

Auth: bearer. The caller becomes the initial owner. New organizations start on Free.

Request:

```json
{ "name": "Cedar Lane Farms", "slug": "cedar-lane-farms" }
```

Response `201`:

```ts
{ organization: Organization }
```

Handled errors: `validation_error` (400), `slug_already_exists` (409), `unauthorized` (401), `internal_error` (500).

### `GET /api/orgs/{orgId}/dashboard`

Auth: bearer; active member.

Response `200`:

```ts
{
  organization: Organization;
  currentSeason: Season | null;
  metrics: {
    activeFieldCount: number; totalAcreage: number;
    seasonProgressPercent: number | null; activitiesThisSeason: number;
    inputCost: number; harvestValue: number; netValue: number;
    yieldPerAcre: number | null;
  };
  recentActivities: Activity[];
  cropProgress: Array<{
    crop: string; acreage: number; fieldCount: number; daysToHarvest: number | null;
  }>;
  limits: { maxFields: number | null };
}
```

`recentActivities` contains at most eight records, newest first. `seasonProgressPercent` is clamped to `0..100`. `maxFields` is `3` on Free and `null` on Pro.

Handled errors: `unauthorized` (401), `forbidden` (403), `not_found` (404), `internal_error` (500).

## Fields

### `GET /api/orgs/{orgId}/fields`

Auth: bearer; active member. Includes active and archived fields.

Response `200`:

```ts
{ fields: Field[] }
```

Handled errors: `unauthorized` (401), `forbidden` (403), `not_found` (404), `internal_error` (500).

### `POST /api/orgs/{orgId}/fields`

Auth: bearer; owner or agronomist.

Request:

```ts
{ name: string; acreage: number; defaultCrop: string; soilType: string | null }
```

Response `201`:

```ts
{ field: Field; seasonRollups: [] }
```

Handled errors: `validation_error` (400), `unauthorized` (401), `forbidden` (403), `field_name_exists` (409), `field_limit_reached` (422), `not_found` (404), `internal_error` (500).

### `GET /api/orgs/{orgId}/fields/{fieldId}`

Auth: bearer; active member.

Response `200`:

```ts
{
  field: Field;
  seasonRollups: Array<{
    seasonId: string; seasonName: string; year: number; crop: string;
    plantedOn: string | null; yieldPerAcre: number | null; inputCost: number;
    harvestValue: number; netValue: number; priorYieldDeltaPercent: number | null;
  }>;
}
```

Handled errors: `unauthorized` (401), `forbidden` (403), `not_found` (404), `internal_error` (500).

### `PATCH /api/orgs/{orgId}/fields/{fieldId}`

Auth: bearer; owner or agronomist.

Request:

```ts
{ name: string; acreage: number; defaultCrop: string; soilType: string | null }
```

Response `200` has the same shape as `GET /api/orgs/{orgId}/fields/{fieldId}`.

Handled errors: `validation_error` (400), `unauthorized` (401), `forbidden` (403), `not_found` (404), `field_name_exists` (409), `internal_error` (500).

### `DELETE /api/orgs/{orgId}/fields/{fieldId}`

Auth: bearer; owner or agronomist. Archives rather than hard-deletes the field.

Response `204`: no body.

Handled errors: `unauthorized` (401), `forbidden` (403), `not_found` (404), `field_already_archived` (409), `internal_error` (500).

## Seasons

### `GET /api/orgs/{orgId}/seasons`

Auth: bearer; active member. Returns newest year first.

Response `200`:

```ts
{ seasons: Season[] }
```

Handled errors: `unauthorized` (401), `forbidden` (403), `not_found` (404), `internal_error` (500).

### `POST /api/orgs/{orgId}/seasons`

Auth: bearer; owner or agronomist.

Request:

```ts
{ year: number; name: string; startsOn: string; endsOn: string }
```

Response `201`:

```ts
{ season: Season }
```

Handled errors: `validation_error` (400), `unauthorized` (401), `forbidden` (403), `not_found` (404), `season_year_exists` (409), `invalid_season_dates` (422), `internal_error` (500).

## Activities

### `GET /api/orgs/{orgId}/fields/{fieldId}/activities?seasonId={seasonId}`

Auth: bearer; active member. `seasonId` is optional. Results are newest first.

Response `200`:

```ts
{ activities: Activity[] }
```

Handled errors: `validation_error` (400), `unauthorized` (401), `forbidden` (403), `not_found` (404), `internal_error` (500).

### `POST /api/orgs/{orgId}/fields/{fieldId}/activities`

Auth: bearer; owner or agronomist.

Request:

```ts
{
  seasonId: string; type: ActivityType; activityDate: string;
  quantity: number | null; quantityUnit: string | null;
  costAmount: number | null; revenueAmount: number | null; notes: string | null;
}
```

Response `201`:

```ts
{ activity: Activity }
```

Handled errors: `validation_error` (400), `unauthorized` (401), `forbidden` (403), `not_found` (404), `season_field_mismatch` (422), `internal_error` (500).

### `PATCH /api/orgs/{orgId}/activities/{activityId}`

Auth: bearer; owner or agronomist. Request is the same complete activity input used by `POST`.

Response `200`:

```ts
{ activity: Activity }
```

Handled errors: `validation_error` (400), `unauthorized` (401), `forbidden` (403), `not_found` (404), `season_field_mismatch` (422), `internal_error` (500).

### `DELETE /api/orgs/{orgId}/activities/{activityId}`

Auth: bearer; owner or agronomist.

Response `204`: no body.

Handled errors: `unauthorized` (401), `forbidden` (403), `not_found` (404), `internal_error` (500).

## Insights, reports, and export

### `GET /api/orgs/{orgId}/insights`

Auth: bearer; active member.

Response `200` (the first three properties follow the wiki payload exactly; the remaining series complete the documented chart set):

```ts
{
  selectedSeasonId: string;
  totals: {
    activeFields: number; totalAcreage: number; inputCost: number;
    harvestValue: number; netValue: number;
  };
  yieldBySeason: Array<{ year: number; crop: string; yieldPerAcre: number }>;
  costVsValue: Array<{ year: number; inputCost: number; harvestValue: number }>;
  cropMix: Array<{ crop: string; acreage: number }>;
  fieldNetValue: Array<{ fieldId: string; fieldName: string; netValue: number }>;
  activityCountByType: Array<{ type: ActivityType; count: number }>;
}
```

Handled errors: `unauthorized` (401), `forbidden` (403), `not_found` (404), `internal_error` (500).

### `GET /api/orgs/{orgId}/seasons/{seasonId}/report`

Auth: bearer; active member and Pro entitlement.

Response `200`:

```ts
{
  organization: { id: string; name: string; plan: Plan };
  season: Season;
  generatedAt: string;
  summary: {
    activeFields: number; totalAcreage: number; inputCost: number;
    harvestValue: number; netValue: number; averageYieldPerAcre: number | null;
  };
  fields: Array<{
    fieldId: string; fieldName: string; crop: string; acreage: number;
    activityCount: number; inputCost: number; harvestValue: number;
    yieldPerAcre: number | null;
  }>;
  activitySummary: Array<{ type: ActivityType; count: number }>;
  activities: Activity[];
}
```

Handled errors: `unauthorized` (401), `forbidden` (403), `not_found` (404), `pro_required` (403), `internal_error` (500).

### `GET /api/orgs/{orgId}/exports/activities.csv?seasonId={seasonId}`

Auth: bearer; owner or agronomist and Pro entitlement.

Response `200`: `text/csv; charset=utf-8`, streamed attachment. Header columns, in order:

```text
organization,field,season,crop,activity_type,activity_date,quantity,quantity_unit,cost_amount,revenue_amount,notes
```

Handled errors use the shared JSON envelope: `validation_error` (400), `unauthorized` (401), `forbidden` (403), `not_found` (404), `pro_required` (403), `internal_error` (500).

## Members

### `GET /api/orgs/{orgId}/members`

Auth: bearer; active member.

Response `200`:

```ts
{
  members: Array<{
    userId: string; displayName: string; email: string; role: Role; joinedAt: string;
  }>;
}
```

Handled errors: `unauthorized` (401), `forbidden` (403), `not_found` (404), `internal_error` (500).

### `POST /api/orgs/{orgId}/members`

Auth: bearer; owner only. The email must identify an existing registered user in v1.

Request:

```ts
{ email: string; role: Role }
```

Response `201`:

```ts
{ member: { userId: string; displayName: string; email: string; role: Role; joinedAt: string } }
```

Handled errors: `validation_error` (400), `unauthorized` (401), `forbidden` (403), `user_not_found` (404), `already_a_member` (409), `internal_error` (500).

### `PATCH /api/orgs/{orgId}/members/{userId}`

Auth: bearer; owner only.

Request:

```ts
{ role: Role }
```

Response `200`:

```ts
{ member: { userId: string; displayName: string; email: string; role: Role; joinedAt: string } }
```

Handled errors: `validation_error` (400), `unauthorized` (401), `forbidden` (403), `not_found` (404), `last_owner_required` (422), `internal_error` (500).

### `DELETE /api/orgs/{orgId}/members/{userId}`

Auth: bearer; owner only.

Response `204`: no body.

Handled errors: `unauthorized` (401), `forbidden` (403), `not_found` (404), `last_owner_required` (422), `cannot_remove_self` (422), `internal_error` (500).

## Plan and billing

### `GET /api/orgs/{orgId}/billing`

Auth: bearer; active member. This payload follows the billing wiki exactly.

Response `200`:

```ts
{
  plan: Plan;
  limits: { maxFields: number | null; csvExportEnabled: boolean; seasonReportEnabled: boolean };
  usage: { activeFieldCount: number };
  history: Array<{
    fromPlan: Plan; toPlan: Plan; changedBy: string; changedAt: string;
  }>;
}
```

Handled errors: `unauthorized` (401), `forbidden` (403), `not_found` (404), `internal_error` (500).

### `POST /api/orgs/{orgId}/billing/upgrade`

Auth: bearer; owner only. Request has no body. This is an in-app entitlement change; no payment is processed.

Response `200`:

```ts
{ plan: "pro"; changedAt: string }
```

Handled errors: `unauthorized` (401), `forbidden` (403), `not_found` (404), `already_on_plan` (409), `internal_error` (500).

### `POST /api/orgs/{orgId}/billing/downgrade`

Auth: bearer; owner only. Request has no body. This is an in-app entitlement change; no refund or payment operation occurs.

Response `200`:

```ts
{ plan: "free"; changedAt: string }
```

Handled errors: `unauthorized` (401), `forbidden` (403), `not_found` (404), `already_on_plan` (409), `too_many_active_fields_for_free` (422), `internal_error` (500).

For `too_many_active_fields_for_free`, the message must tell the caller how many active fields exist and that all but three must be archived before retrying.
