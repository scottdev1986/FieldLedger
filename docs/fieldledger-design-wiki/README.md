# FieldLedger Design Wiki

**FieldLedger** is a portfolio-grade multi-tenant farm operations SaaS design package. It is structured as a Karpathy-style LLM Wiki: raw source material is preserved, the maintained design lives as interlinked Markdown pages, and the schema file tells future LLM/code agents how to keep the wiki consistent.

## Start here

1. Open [`index.md`](index.md) for the table of contents.
2. Read [`wiki/00-executive-summary.md`](wiki/00-executive-summary.md) for the high-level design.
3. Read [`wiki/auth-rls.md`](wiki/auth-rls.md) and [`adr/0006-self-contained-postgres-first-party-auth.md`](adr/0006-self-contained-postgres-first-party-auth.md) for the main technical differentiator.
4. Use [`printable/fieldledger-technical-design-single-file.md`](printable/fieldledger-technical-design-single-file.md) when you need one flat technical design document.

## Package layout

```text
fieldledger-design-wiki/
  README.md
  AGENTS.md                         # wiki schema / maintenance rules
  index.md                          # content catalog
  log.md                            # chronological wiki log
  raw/                              # immutable source material
  wiki/                             # maintained design pages
  adr/                              # architecture decision records
  diagrams/                         # Mermaid source diagrams
  templates/                        # implementation-ready starter files
  docs/                             # source/documentation link map
  printable/                        # compiled single-file TDD
```

## What this document is optimized to prove

FieldLedger demonstrates a complete SaaS MVP pattern: organizations, roles, billing, entitlements, deterministic demo data, C# backend work, and database-level row security. The browser can render role-aware and plan-aware UI, but the API and database remain the source of truth.

## Wiki maintenance model

This package follows three layers:

- **Raw sources:** unchanged inputs, briefs, meeting notes, and future research.
- **Wiki:** maintained synthesis pages that are safe to edit as the design evolves.
- **Schema:** [`AGENTS.md`](AGENTS.md), which defines page conventions, cross-linking, lint checks, and update workflows.

The wiki also includes [`index.md`](index.md) for navigation and [`log.md`](log.md) for chronological traceability.
