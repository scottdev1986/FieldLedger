# FieldLedger Wiki Log

## [2026-07-09] ingest | Initial FieldLedger product brief

Created the initial Karpathy-style design wiki from the FieldLedger product brief. Preserved the original brief in `raw/`, created the maintained design pages in `wiki/`, added ADRs for the major architecture choices, added Mermaid diagrams, added templates, and compiled a single-file printable technical design document.

## [2026-07-09] query | Package as downloadable zip

Packaged the wiki as a zip artifact with raw/source/schema/wiki/index/log structure, official documentation links, and implementation-ready starter templates.

## [2026-07-09] maintenance | Add missing wiki routing map

Added `references/wiki-map.md` as the task-to-page routing file referenced by the FieldLedger design-wiki skill. Registered the `references/` directory in `AGENTS.md`, linked the map from `index.md`, and updated `MANIFEST.txt`.

## [2026-07-09] implementation | Align Compose scaffold with Phase 0

Updated the Docker local-development page and Compose template to put `stripe-cli` behind a `stripe` profile, matching the initial scaffold so `docker compose up --build` can run the basic web/API services before Stripe credentials are configured.

## [2026-07-09] redesign | Self-contained stack: local Postgres, first-party auth, Stripe removed

Redesigned the wiki for a fully self-contained stack per user direction: local PostgreSQL 17 container replaces the hosted Supabase project, first-party email/password auth with API-issued HS256 JWTs replaces Supabase Auth, and Stripe is removed entirely while Free/Pro entitlement gating is preserved through an in-app, owner-only, audited plan-change path (`app.set_org_plan`). Added ADR 0006 and ADR 0007; marked ADR 0002 and ADR 0004 superseded; annotated ADR 0003 with the `app.user_id` config-key rename. Rewrote all core wiki pages, all four diagrams, and all templates; updated `index.md`, `README.md`, `references/wiki-map.md`, `docs/source-map.md`, and `MANIFEST.txt`; regenerated the printable single-file document.

## [2026-07-09] implementation | Auth data-access functions (migration 0005)

Implementation verification showed the v2 `users` policies blocked pre-auth login/register lookups and peer display names for the members roster. Added `db/migrations/0005_auth_access.sql` (security definer `app.get_user_for_login`, `app.register_user`, `app.shares_org_with`; `users_read_org_peers` policy; column-level `users` grant excluding `password_hash`) and documented the design in `wiki/auth-rls.md`. RLS assertion suite still passes.

## [2026-07-09] implementation | Full v2 build complete and verified

Implementation of the v2 design finished: database (6 migrations, RLS suite green), ASP.NET Core API (20/20 tests including live-database integration tests), deterministic seeder with migrate mode, and the complete Next.js frontend per the design brief. Verified end-to-end from zero via Docker Compose and a full browser walkthrough (owner/agronomist/viewer roles, Free→Pro in-app upgrade with audit trail, gated report/CSV unlock). Added migration `0006_auth_function_text_params.sql` (auth functions take text params — citext params fail typed-driver function resolution) and synced the printable document's auth section. CI runs migrations, RLS assertions, and the full test suite against a Postgres 17 service container.

## [2026-07-09] maintenance | Root .env.example removed; compose defaults are canonical

The root `.env.example` was removed per user direction: every variable has a working local default in `docker-compose.yml`, and an untracked `.env` remains an optional override. The wiki's `templates/.env.example` stays as the variable reference. Updated docker-local-dev, demo-script, acceptance-criteria, auth-rls, risks, and build-plan accordingly, plus the printable document.
