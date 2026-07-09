# Source Map

This page collects the main official documentation links used by the FieldLedger design.

## Karpathy-style LLM Wiki pattern

- [Andrej Karpathy — LLM Wiki gist](https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f)

Useful design ideas adopted here:

- raw sources are preserved,
- maintained wiki pages are generated/updated separately,
- a schema file defines conventions,
- `index.md` is content-oriented,
- `log.md` is chronological,
- cross-references make the knowledge base compounding.

## PostgreSQL and RLS

- [PostgreSQL Row Security Policies](https://www.postgresql.org/docs/17/ddl-rowsecurity.html)
- [PostgreSQL CREATE POLICY](https://www.postgresql.org/docs/17/sql-createpolicy.html)
- [PostgreSQL SET / set_config and transaction-local settings](https://www.postgresql.org/docs/17/sql-set.html)
- [PostgreSQL advisory locks](https://www.postgresql.org/docs/17/explicit-locking.html#ADVISORY-LOCKS)
- [Postgres official Docker image (initdb scripts)](https://hub.docker.com/_/postgres)

## ASP.NET Core

- [Configure JWT bearer authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication)
- [ASP.NET Core authentication overview](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)
- [ASP.NET Core PasswordHasher](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1)
- [Npgsql documentation](https://www.npgsql.org/doc/index.html)

## Next.js and frontend

- [Next.js App Router docs](https://nextjs.org/docs/app)
- [TanStack Query docs](https://tanstack.com/query/latest/docs/framework/react/overview)

## Docker

- [Docker Compose environment variables](https://docs.docker.com/compose/how-tos/environment-variables/set-environment-variables/)
- [Docker Compose services reference](https://docs.docker.com/reference/compose-file/services/)
- [Compose depends_on conditions](https://docs.docker.com/reference/compose-file/services/#depends_on)

## Reporting

- [QuestPDF quick start](https://www.questpdf.com/quick-start.html)

## Removed integrations (historical)

Supabase and Stripe documentation links were removed on 2026-07-09 when
[ADR 0006](../adr/0006-self-contained-postgres-first-party-auth.md) and
[ADR 0007](../adr/0007-in-app-plan-management-no-external-billing.md) made the stack
fully self-contained. See superseded [ADR 0002](../adr/0002-use-aspnet-api-with-supabase-rls.md)
and [ADR 0004](../adr/0004-stripe-webhooks-as-entitlement-source.md) for the prior design.
