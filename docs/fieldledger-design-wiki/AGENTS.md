# FieldLedger Wiki Schema and Agent Instructions

This file defines how an LLM or coding agent should maintain this design wiki.

## Core principle

Do not treat the technical design as one static blob. Maintain it as a compiled, interlinked Markdown wiki. Add new information to the smallest appropriate page, update related pages, record the change in `log.md`, and keep `index.md` current.

## Directory roles

| Directory | Ownership | Rule |
|---|---|---|
| `raw/` | Human/source-owned | Preserve source material. Do not rewrite except to add a header or metadata wrapper. |
| `wiki/` | Maintained synthesis | Update freely as the design evolves. Prefer focused pages over giant pages. |
| `adr/` | Decision log | Append new ADRs for major irreversible decisions. Do not rewrite accepted ADR history except to mark superseded. |
| `diagrams/` | Diagram source | Mermaid source-of-truth diagrams. Keep diagram filenames stable. |
| `templates/` | Starter implementation snippets | Keep examples build-oriented but not secret-bearing. |
| `references/` | Agent routing aids | Keep task-to-page maps and maintenance references current with the index. |
| `docs/` | Source map | Keep official documentation and reference links here. |
| `printable/` | Generated/compiled | Regenerate from wiki pages when structure changes. |

## Page frontmatter

Every maintained wiki page should begin with YAML frontmatter:

```yaml
---
title: Human-readable page title
status: draft | proposed | accepted | superseded
tags: [fieldledger, architecture]
last_updated: YYYY-MM-DD
related: [architecture, auth-rls]
---
```

## Cross-linking rules

Use Obsidian-compatible wiki links for conceptual relationships:

```md
See [[auth-rls]] and [[billing-entitlements]].
```

Use relative Markdown links for filesystem navigation:

```md
See [Auth and RLS](auth-rls.md).
```

When creating a new page, add backlinks from at least one existing page and add the page to `index.md`.

## Source policy

- Put raw notes, briefs, and copied specs in `raw/`.
- Put official documentation links in `docs/source-map.md`.
- Prefer official docs over blog posts for implementation details.
- If a page depends on an external fact that may change, link to the relevant official documentation.
- Do not store API keys, service-role keys, database passwords, webhook secrets, or live credentials.

## Update workflow

### Ingest

When new source material is added:

1. Read the new source.
2. Extract durable facts, constraints, decisions, assumptions, and open questions.
3. Update existing wiki pages before creating new pages.
4. Create a new page only when the concept deserves independent linking.
5. Update `index.md`.
6. Append a `log.md` entry.

### Query

When answering design questions:

1. Read `index.md` first.
2. Read the smallest set of relevant pages.
3. Return an answer with page links.
4. If the answer creates durable design knowledge, file it back into the wiki.

### Lint

Run a periodic health check for:

- stale claims,
- unlinked/orphan pages,
- duplicated endpoint definitions,
- mismatched role rules,
- undocumented environment variables,
- diagrams that conflict with prose,
- ADRs not reflected in design pages,
- accepted decisions without tests.

## Naming conventions

- Wiki pages: `lower-kebab-case.md`
- ADRs: `0001-short-title.md`
- Diagrams: `short-purpose.mmd`
- Templates: `purpose.example.ext` or descriptive executable-style filename

## Style conventions

- Prefer implementation-ready specificity.
- Use tables for role matrices, endpoints, plans, and test cases.
- Keep “why” close to “what.”
- Put hard security claims in `auth-rls.md` and ADRs.
- Use `Assumptions` and `Open questions` sections when needed.
