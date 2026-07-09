# Raw Source: FieldLedger Product Brief

**Source type:** User-provided project brief  
**Ingested:** 2026-07-09  
**Status:** Immutable source material

---

## 1. FieldLedger — multi-tenant farm operations SaaS *(build first)*

**Pitch:** the public twin of the gated agricultural reporting platform.
"I built ag-operations reporting professionally; that code is gated, so I built this one for you to read." Closes the examples-ask on the biggest job category (SaaS MVPs with orgs/roles/billing) while showing C# **and** Supabase RLS in one artifact.

**Answers jobs like:** Next.js/Supabase SaaS MVPs (24, 47, 98, 102, 121, 125, 140), takeover/audit posts where RLS is the landmine, .NET posts (155-style), anything with subscription gating.

**The product.** A farm business tracks its fields and operations across seasons and pays for the size it needs.

- **Entities**: organization (a farm business) → members (roles: *owner* = billing/invites, *agronomist* = edit data, *viewer* = read-only) → fields (name, acreage, crop) → seasons → activity log per field/season (planting, spraying, irrigation, harvest — dates, quantities, costs).
- **Pages**: org dashboard (fields at a glance, season progress), field detail (activity timeline, per-season yield), insights (yield/acre, input cost vs harvest value, charts across seasons), season report (compiled org numbers — public echo of the gated PDF-report work), members & roles, billing.
- **Billing**: Stripe test mode. Free = 3 fields; Pro = unlimited + CSV export + season report. Webhooks flip entitlements server-side; the UI reflects the change live. Never trusts the browser about plan state.
- **Auth/data**: Supabase free tier (hosted) provides Postgres, Auth, and RLS. The React app logs in against Supabase Auth; the **ASP.NET Core Web API** validates the Supabase JWT and forwards the user's claims into Postgres per request, so **RLS enforces org isolation and role rights in the database even though all traffic goes through the C# API** — RLS as defense-in-depth behind an API layer, which is a stronger talking point than client-direct RLS.

**Docker compose services:** `web` (React + Tailwind), `api` (ASP.NET Core Web API), `stripe-cli` (forwards Stripe test webhooks to the local api — this is how webhooks work with zero deployment), `seeder` (below). External: Supabase free project, Stripe test keys — both via `.env`.

**Seeding (the demo-data answer):** a dedicated `seeder` compose service — a C# console app run as `docker compose run seeder`. It (1) creates the demo accounts through the Supabase Admin API (owner / agronomist / viewer logins, two separate orgs), (2) generates three seasons of realistic data from a crop-calendar generator — corn/soy/wheat planting windows, spray passes, harvest yields and costs with seeded randomness so numbers look real but the run is deterministic, (3) is idempotent (marker row; safe to re-run). Demo credentials documented in the README.

**Hard problems showcased:** RLS policy design for org + role, JWT claim forwarding from a C# API into Postgres, server-side entitlement enforcement from Stripe webhooks.

**2-minute demo:** log in as demo owner → two orgs exist, switch between → run a cross-org query in Supabase SQL editor as that user: zero rows → upgrade with Stripe test card `4242…` → stripe-cli delivers the webhook, field limit flips live → open season report full of three years of data.

**Done when:** compose up + seeder gives the full demo from zero, README has an RLS policy walkthrough section, CI green.
