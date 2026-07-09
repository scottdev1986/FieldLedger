---
title: Frontend Design
status: proposed
tags: [fieldledger, frontend, nextjs, react]
last_updated: 2026-07-09
related: [api-design, domain-model, billing-entitlements]
---

# Frontend Design

The frontend is a Next.js/React app styled with Tailwind. It authenticates against the API's first-party `/api/auth` endpoints and calls the ASP.NET Core API for all application data.

## Frontend rules

1. Authenticate against the API's `/api/auth` endpoints (`register`, `login`, `me`).
2. Store the access token in `localStorage` plus memory via `lib/auth.ts` and an auth provider. This is an accepted portfolio-v1 tradeoff versus an httpOnly-cookie BFF pattern (see [ADR 0006](../adr/0006-self-contained-postgres-first-party-auth.md)).
3. Never call the database from the browser.
4. Render role-aware UI, but treat it as convenience only.
5. Refetch entitlements after plan mutations resolve.

## Suggested libraries

| Concern | Suggested library |
|---|---|
| Auth | first-party `lib/auth.ts` + React context |
| Server state | TanStack Query |
| Forms | React Hook Form + Zod |
| Charts | Recharts |
| Tables | TanStack Table or simple components |
| Styling | Tailwind CSS |

## Route map

```text
/login
/orgs
/orgs/[orgId]
/orgs/[orgId]/fields
/orgs/[orgId]/fields/[fieldId]
/orgs/[orgId]/insights
/orgs/[orgId]/seasons/[seasonId]/report
/orgs/[orgId]/members
/orgs/[orgId]/billing
```

## Layout structure

```text
<AppShell>
  <TopNav />
  <OrgSwitcher />
  <PlanBadge />
  <Sidebar />
  <main>{page}</main>
</AppShell>
```

## Auth behavior

- Login form calls `POST /api/auth/login` and receives `{ accessToken, user }`.
- `lib/auth.ts` stores the token in `localStorage` and in-memory state; the auth provider exposes the current session to the tree.
- API client reads the stored token before each request.
- On `401`, clear the session and route to `/login`.
- On `403`, show role/plan-specific message.

## API client sketch

```ts
import { getAccessToken } from './auth';

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getAccessToken();

  const response = await fetch(`${process.env.NEXT_PUBLIC_API_BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {})
    }
  });

  if (!response.ok) {
    throw await response.json();
  }

  return response.json();
}
```

## Pages

### Login

- Email/password login against `/api/auth/login`.
- One-click demo-credential buttons for the three seeded users (owner, agronomist, viewer).
- Redirect to `/orgs` after login.

### Org dashboard

Cards:

- active fields,
- acreage,
- current season,
- recent activity,
- yield per acre,
- input cost,
- harvest value,
- plan/field limit status.

### Field detail

Sections:

- field metadata,
- season selector,
- activity timeline,
- per-season yield,
- cost/revenue rollup.

### Insights

Charts:

- yield per acre by season,
- input cost vs harvest value,
- crop mix by acreage,
- field profitability.

### Season report

- Free users see Pro upsell.
- Pro users see report preview and PDF download.
- Report endpoint must still enforce Pro.

### Members and roles

- Owner-only mutation controls.
- Non-owners can view read-only roster or be redirected, depending on UX choice.

### Billing

- Plan card showing current plan.
- Usage meter (n of 3 active fields on Free).
- Owner-only Upgrade/Downgrade buttons calling `POST /api/orgs/{orgId}/billing/upgrade` and `.../downgrade`.
- Plan-change history list from `GET /api/orgs/{orgId}/billing`.
- No polling: the plan change is synchronous, so refetch billing state after the mutation resolves. A downgrade with more than 3 active fields surfaces the `422 too_many_active_fields_for_free` message and prompts the owner to archive fields.

## Component naming

```text
features/
  auth/
  orgs/
  fields/
  seasons/
  activities/
  insights/
  reports/
  members/
  billing/
components/
  app-shell/
  charts/
  forms/
  ui/
lib/
  api-client.ts
  auth.ts
  query-keys.ts
```

## UX proof points for demo

- Owner sees plan and members controls.
- Agronomist sees data-edit controls but not plan/member management.
- Viewer sees read-only pages.
- Free org shows 3-field limit.
- Owner clicks Upgrade and the plan badge flips instantly, with the audit row appearing in plan-change history.
- Pro org shows exports and reports.
