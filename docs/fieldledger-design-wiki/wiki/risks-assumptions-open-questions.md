---
title: Risks, Assumptions, and Open Questions
status: accepted
tags: [fieldledger, risks, assumptions]
last_updated: 2026-07-09
related: [architecture, auth-rls, billing-entitlements]
---

# Risks, Assumptions, and Open Questions

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| API accidentally bypasses RLS | Cross-tenant data exposure | Connect as `fieldledger_api` (no `BYPASSRLS`); never use the migrator/table-owner connection for user data. |
| Claims leak across pooled DB connections | User context contamination | Use transaction-scoped settings (`set_config(..., true)`) and dispose transactions correctly. |
| RLS policies become complex | Bugs and maintenance drag | Centralize helper functions and add direct policy tests. |
| Browser token storage in `localStorage` | XSS exposure of the session token | Accepted portfolio-v1 tradeoff, documented; short token lifetime is a config option (`AUTH_TOKEN_LIFETIME_MINUTES`). |
| Dev-default secrets in `.env.example` | Copied into a real deployment | Documented as local-only; rotate all secrets and passwords in real deploys. |
| Free limit only in UI | Easy bypass | Enforce in API and DB trigger. |
| Seeder duplicates data | Messy demo | Use marker row, upserts, deterministic keys. |
| PDF library licensing changes | Future production constraint | Document dependency and license assumptions. |

## Assumptions

- A local PostgreSQL 17 container is sufficient for the portfolio demo.
- Simulated in-app plan changes are sufficient to prove entitlement gating.
- The product does not need GIS field boundaries in v1.
- The season report can start as HTML/Markdown and later become PDF.
- Deterministic demo data is more valuable than randomized realism.
- One organization membership row is the authoritative role source.

## Open questions

1. Should viewer be allowed to generate season report if the org is Pro? Current design says yes.
2. Should CSV export be owner-only or owner/agronomist? Current design says owner/agronomist.
3. Should the fourth seeded North Fork field start archived so Free-plan limit is visually clean?
4. Should the first PDF implementation use QuestPDF or HTML-to-PDF?
5. Should membership invites be real email invitations or direct user assignment for portfolio v1?
6. Should the API use Dapper, EF Core, or raw Npgsql? Raw SQL/Dapper makes RLS and query shapes explicit.
7. Should `POST /api/auth/register` be open or invite-only in demo mode?

## Recommended decisions for v1

- No polling is needed; the upgrade mutation is synchronous, so refetch after it resolves.
- Use direct demo user assignment instead of email invitation delivery.
- Use Dapper or raw Npgsql for clarity.
- Generate report HTML first, PDF second.
- Archive the fourth seeded field until upgrade, then let owner create/reactivate it after Pro.
