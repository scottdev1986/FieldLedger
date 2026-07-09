---
title: ADR 0003 - Transaction-Scoped Claim Forwarding
status: accepted
last_updated: 2026-07-09
tags: [adr, postgres, security]
related: [auth-rls]
---

# ADR 0003: Transaction-Scoped Claim Forwarding

## Status

Accepted.

## Context

Postgres RLS policies need request/user context. Because the ASP.NET Core API is issuing SQL directly, it must provide that context explicitly.

Connection pooling makes session-level settings risky because values can persist across requests.

## Decision

For every authenticated request, open a transaction and set user context using transaction-local settings. Run all tenant-scoped SQL inside that transaction.

Conceptual SQL:

```sql
begin;
set local role authenticated;
select set_config('request.jwt.claim.sub', @user_id, true);
select set_config('request.jwt.claims', @claims_json, true);
-- query
commit;
```

> Note (2026-07-09): the decision itself is unchanged, but after
> [ADR 0006](0006-self-contained-postgres-first-party-auth.md) removed PostgREST/Supabase
> compatibility, the config key is `app.user_id` (single setting; no claims JSON). See
> [[auth-rls]] for the current SQL.

## Consequences

Positive:

- Avoids context leakage across pooled connections.
- Keeps RLS policies simple and testable.
- Mirrors PostgREST-style request claims enough for helper functions.

Tradeoffs:

- All handlers need to use the DB session abstraction.
- Long-running streaming endpoints need careful transaction handling.
- System/webhook jobs need a separate controlled path.
