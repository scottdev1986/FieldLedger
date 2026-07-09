# FieldLedger — Frontend Design Brief

Prescriptive spec for the FieldLedger web UI (Next.js App Router, Tailwind CSS v4, TypeScript, React).
Implement exactly as written; where a value is given, use that value.

---

## 1. Design direction: "The Agronomist's Ledger"

FieldLedger is a working record book for farm operations, so the UI borrows from two credible
sources: **the printed field ledger** (warm paper tones, hairline rules, tabular numerals,
small-caps column labels) and **modern data tooling** (dense-but-calm tables, restrained charts,
crisp hierarchy). It is a **light-theme-only** application. No dark mode in v1.

Personality words: *credible, calm, seasonal, data-forward*. The one memorable thing: everything
numeric is set in a monospace face on warm paper, so screens read like a well-kept ledger, not a
generic admin template.

Hard rules that define the look:

- Warm bone-paper app background (`#F6F4EE`), pure white cards, hairline warm-gray borders.
  Depth comes from borders + very soft shadows, never heavy elevation.
- Fir-green brand color used sparingly: primary buttons, active nav, links, focus rings. Large
  areas of green are forbidden except the login page's left panel.
- All metrics, dates, acreages, currency, and axis labels use the mono font with
  `font-variant-numeric: tabular-nums`.
- Section labels are 11px uppercase letterspaced "overlines" (see 3.2).
- Decorative texture: a single inline-SVG contour-line pattern (thin topographic curves, 4% opacity,
  stroke `#2F5227`) used ONLY on the login left panel, empty states, and the Pro-upsell card.
  Define it once as `components/ContourPattern.tsx`. No other illustration anywhere.

## 2. Design tokens (Tailwind v4 `@theme` in `app/globals.css`)

Fonts: load with `next/font/google` (self-hosted at build time — zero runtime network requests):
**Fraunces** (headings; variable, use `opsz` auto, weights 500–650), **Instrument Sans** (UI/body,
400/500/600), **Spline Sans Mono** (numerals/code, 400/500). Expose each via a CSS variable in the
root layout (`--font-fraunces`, `--font-instrument`, `--font-splinemono`). No `@import` of fonts,
no `fonts.googleapis.com` anywhere.

```css
@theme {
  /* Brand (fir green) */
  --color-brand-50:  #F1F5EC;
  --color-brand-100: #DFE9D3;
  --color-brand-200: #BED4A8;
  --color-brand-300: #97B97C;
  --color-brand-400: #6F9C54;
  --color-brand-500: #517F3B;
  --color-brand-600: #3E662C;  /* primary actions, links, active nav */
  --color-brand-700: #325224;
  --color-brand-800: #2A431F;
  --color-brand-900: #22361B;

  /* Warm neutrals (paper + ink) */
  --color-paper:      #F6F4EE;  /* app background */
  --color-surface:    #FFFFFF;  /* cards, tables, modals */
  --color-sunken:     #EFECE3;  /* table header rows, wells, skeleton base */
  --color-line:       #E4E0D5;  /* hairline borders */
  --color-line-strong:#CFC9BA;  /* input borders, dividers needing emphasis */
  --color-ink:        #21281E;  /* primary text */
  --color-ink-soft:   #565E50;  /* secondary text */
  --color-ink-faint:  #8A9082;  /* placeholders, disabled, tertiary */

  /* Semantic */
  --color-success: #3E7C46;  --color-success-bg: #E8F2E7;
  --color-warn:    #9A6B14;  --color-warn-bg:    #F7EDD8;
  --color-danger:  #AE3F2B;  --color-danger-bg:  #F9E9E4;
  --color-info:    #2F6E85;  --color-info-bg:    #E4F0F4;

  /* Chart categorical (in series order; all AA-visible on white) */
  --color-chart-1: #4C7A34;  /* crop green */
  --color-chart-2: #C4862B;  /* harvest gold */
  --color-chart-3: #2F7E93;  /* irrigation teal */
  --color-chart-4: #A8512F;  /* clay */
  --color-chart-5: #6E5F9E;  /* plum */
  --color-chart-6: #7C8148;  /* olive */

  /* Activity-type accents (badges, timeline dots) */
  --color-act-planting:   #4C7A34;
  --color-act-spraying:   #6E5F9E;
  --color-act-irrigation: #2F7E93;
  --color-act-fertilizer: #8A6420;
  --color-act-harvest:    #C4862B;
  --color-act-note:       #77716A;

  /* Type */
  --font-display: var(--font-fraunces), Georgia, serif;
  --font-sans:    var(--font-instrument), system-ui, sans-serif;
  --font-mono:    var(--font-splinemono), ui-monospace, monospace;

  /* Radii */
  --radius-sm: 6px;   /* badges-as-chips, small controls */
  --radius-md: 8px;   /* buttons, inputs */
  --radius-lg: 12px;  /* cards, modals, drawers */

  /* Shadows (warm, faint) */
  --shadow-card:  0 1px 2px rgb(33 40 30 / 0.05);
  --shadow-pop:   0 4px 12px rgb(33 40 30 / 0.10);   /* menus, popovers */
  --shadow-modal: 0 12px 32px rgb(33 40 30 / 0.16);
}
```

Spacing rhythm: 4px base grid. Card padding `p-5` (20px). Gap between cards `gap-4` (16px).
Page gutter `px-6` desktop / `px-4` mobile. Vertical section spacing `space-y-6` (24px).
Content max width `max-w-[1200px] mx-auto`. Body text 14px/1.5; secondary 13px; tables 13px.

## 3. Layout system

### 3.1 App shell (`app/orgs/[orgId]/layout.tsx`)
- **Sidebar**: fixed left, `w-[260px]`, background `--color-paper`, right border `--color-line`.
  Top→bottom: (1) wordmark "FieldLedger" in Fraunces 600 at 18px with a 20px inline-SVG leaf/ledger
  glyph in brand-600; (2) **org switcher** — a full-width button showing org name + chevron, opens a
  popover menu (shadow-pop, radius-lg) listing the user's orgs with the **plan badge** (see 4.3) next
  to each and a "All organizations" link to `/orgs`; (3) nav items: Dashboard, Fields, Insights,
  Season report, Members, Plan & billing. Nav item: 36px tall, radius-md, 13px/500, ink-soft with
  16px lucide-react icon; active = `bg-brand-50 text-brand-700` with a 2px brand-600 bar on the left
  edge; hover = `bg-sunken`. "Season report" shows a right-aligned lock icon (14px, ink-faint) when
  the org is Free. (4) footer pinned bottom: current org's plan badge + "Upgrade" text link
  (Free orgs, owners only).
- **Topbar**: 56px, surface background, bottom border `--color-line`. Left: page breadcrumb
  (org name / page, 13px ink-soft). Right: user menu button (28px circle avatar with initials on
  brand-100/brand-800, plus display name at ≥1024px) opening a menu: email (non-interactive),
  role in current org, Sign out.
- **Responsive**: below 1024px the sidebar becomes an overlay drawer (same content) toggled by a
  hamburger in the topbar; page content is single-column. Card grids: 4-col ≥1280px, 2-col ≥768px,
  1-col below.

### 3.2 Page header pattern
Every page: overline label (11px, uppercase, tracking `0.08em`, ink-faint, mono) naming the org,
then the page title in Fraunces 600 28px ink, optional one-line description (14px ink-soft), and a
right-aligned primary action (e.g. "Add field"). 24px margin below. Never center titles.

### 3.3 Cards, tables, empty states, skeletons
- **Card**: surface bg, 1px `--color-line` border, radius-lg, shadow-card, p-5. Card title = overline
  style (3.2) — not bold 16px text.
- **Table**: inside a card with `p-0`; header row bg `--color-sunken`, 11px uppercase mono
  letterspaced ink-soft, 10px vertical padding; body rows 44px, 1px `--color-line` row dividers, no
  zebra; numeric columns right-aligned in mono; row hover `bg-paper`; first column is the entity
  name at 500 weight, clickable, brand-700 on hover. Overflow-x scroll on mobile.
- **Empty state**: centered in the card, ContourPattern behind at 4%, a 32px lucide icon in
  ink-faint, one Fraunces 18px line ("No fields yet"), one 13px ink-soft line, and a primary button
  when the viewer's role can create.
- **Skeletons**: match final layout exactly (same card/table shells); blocks are `bg-sunken`
  radius-sm with a slow `animate-pulse` (1.8s). Never spinners for page loads; spinners (16px, 2px
  border) only inside buttons during submits.

## 4. Components

### 4.1 Buttons
Sizes: sm 32px / md 40px (default) / lg 44px height; px 12/16/20; 13/14/14px text at 500; radius-md.
- **primary**: bg brand-600, white text; hover brand-700; active brand-800; disabled 50% opacity +
  `cursor-not-allowed`.
- **secondary**: surface bg, 1px line-strong border, ink text; hover bg-sunken.
- **ghost**: transparent, ink-soft; hover bg-sunken.
- **destructive**: bg `--color-danger`, white; hover darken 8%.
All: focus-visible ring per §7; loading state swaps label for spinner + keeps width (`min-w` lock).

### 4.2 Forms (React Hook Form + Zod)
Label 13px/500 ink, 6px above input. Input: 40px, surface bg, 1px line-strong border, radius-md,
14px text, placeholder ink-faint; focus = brand-600 border + focus ring. Error state: border and
ring `--color-danger`, message below in 12px danger with a 14px `circle-alert` icon; add
`aria-invalid` and `aria-describedby` pointing at the message id. Help text 12px ink-faint.
Selects/dates match input styling. Required fields get no asterisk theater — mark optional ones
with "(optional)" in ink-faint instead.

### 4.3 Badges
Base: 22px tall pill (radius full), 11px/500, px-2, 1px border, uppercase mono for plan/role, normal
case for activity types.
- **Plan**: Free = sunken bg / line-strong border / ink-soft text. Pro = brand-800 bg / white text,
  no border.
- **Role**: owner = warn-bg/warn; agronomist = brand-50/brand-700; viewer = info-bg/info.
- **Activity type**: tinted pill using the `--color-act-*` accents — text at the accent color
  darkened to AA, bg = accent at 12% opacity (`color-mix(in srgb, var(--color-act-x) 12%, white)`),
  border at 25% opacity. Each includes a 6px filled dot in the raw accent color before the label.

### 4.4 Stat cards (dashboard)
Card per §3.3 with: overline metric name; value in mono 28px/500 ink with tabular-nums (units like
"ac" or "$" at 16px ink-soft); below, a 12px ink-soft context line ("across 4 fields") or a delta
chip (mono 11px, success/danger tint, ▲/▼ triangle glyph). No sparkline in v1. Height locks to
content; grid per §3.1.

### 4.5 Activity timeline
Vertical list: 1px `--color-line` rail on the left; each item = a 10px dot in the activity's accent
color (ringed 2px paper) on the rail, then: row 1 = activity-type badge + title (14px/500), row 2 =
13px ink-soft detail (product, rate, crew), row 3 = mono 12px ink-faint date + acreage. 16px gap
between items. Group items under sticky mono overline date-month headers ("APRIL 2026"). Editable
rows (owner/agronomist) show a ghost kebab menu on hover/focus with Edit/Delete.

### 4.6 Modals & drawers
Create/edit forms open in a **right-side drawer**: `w-[440px]` (full-width sheet under 640px),
surface bg, shadow-modal, radius-lg on the left corners, header (Fraunces 18px title + ghost X),
scrollable body, pinned footer with secondary Cancel + primary submit. Destructive confirms use a
centered **modal** `max-w-[400px]`. Overlay: `rgb(33 40 30 / 0.4)`. Both: focus trap, Escape
closes, focus returns to trigger, `role="dialog"` + `aria-labelledby`. Animate 160ms ease-out
slide/fade; respect `prefers-reduced-motion`.

### 4.7 Toasts
Bottom-right stack, max 3. Surface bg, 1px line border, radius-lg, shadow-pop, 4px left accent bar
(success/danger/info), 14px title + optional 13px ink-soft line, auto-dismiss 5s (errors persist
until dismissed), `role="status"` (errors `role="alert"`). No green-filled toasts.

## 5. Recharts rules

- Series colors: `--color-chart-1..6` in order; single-series charts use chart-1 only. Crop-mix
  colors may map crops to specific slots but must stay within the six.
- Grid: horizontal `CartesianGrid` lines only, stroke `#E4E0D5`, no vertical lines, no dashes.
- Axes: no axis lines, no tick lines; tick labels 11px Spline Sans Mono `#8A9082`; Y-axis ticks
  formatted with units ("180 bu", "$12k"); ≤5 Y ticks.
- Bars: radius `[3,3,0,0]`, max bar size 40, category gap ≥ 24%. Lines: 2px, dot r=3 only on
  hover/active. Pie (crop mix): use a donut, inner radius 60%, 2px white stroke between slices,
  legend to the right (bottom on mobile) — never labels-on-slices.
- Tooltip: custom component — surface bg, 1px line border, radius-md, shadow-pop, 12px; label row
  mono, series rows = 8px color square + name + right-aligned mono value. Cursor: `fill: #EFECE3`
  for bars, 1px `#CFC9BA` line for lines.
- Every chart sits in a card with an overline title + 12px ink-soft subtitle; fixed heights:
  280px standard, 220px compact.

## 6. Page-by-page

- **/login** — split screen. Left 45% (hidden <900px): brand-800 background, ContourPattern
  overlay, wordmark top-left in white, and one Fraunces 32px white statement: "The record book for
  every acre." plus a 14px brand-200 subline. Right 55%: paper bg, centered `max-w-[380px]` form —
  email, password, primary "Sign in" (full width). Below a hairline divider labeled "Demo accounts"
  (11px mono overline, centered): three full-width secondary buttons, each with the role badge and
  label: "Sign in as Owner", "…Agronomist", "…Viewer" (one click = login with seeded creds). Footer
  microcopy 12px ink-faint: "Demo environment — data resets on reseed."
- **/orgs** — no sidebar; slim topbar (wordmark + user menu). Page header "Your organizations";
  grid of org cards (2-col ≥768px): org name in Fraunces 20px, role badge + plan badge row, mono
  meta line ("4 fields · 3 seasons"), whole card clickable with hover border brand-300.
- **/orgs/[orgId]** (dashboard) — header per §3.2 (title = org name). Row of 4 stat cards: Active
  fields (Free orgs append mono "of 3"), Total acreage, Activities this season, Est. harvest value.
  Below, two-column (2fr/1fr): "Recent activity" card (timeline §4.5, last 8, "View all" ghost link)
  and a "This season" card (current season name, crop list with act-dots, days-to-harvest mono).
- **Fields list** — header + primary "Add field" (owner/agronomist only; on Free at 3 fields the
  button is replaced per the locked pattern below). Table: Name, Crop (current season), Acreage
  (mono, right), Last activity (badge + mono date), Status. Row click → detail. Empty state §3.3.
- **Field detail** — header: field name + acreage in mono, status badge; meta strip of mono
  facts (soil, planted date). Tabs (underline style, 2px brand-600 active indicator): Activity /
  Seasons. Activity = timeline §4.5 + "Log activity" primary opening the drawer (type select drives
  dynamic Zod fields). Seasons = table of per-season yield: Season, Crop, Yield/acre (mono),
  Harvest value (mono), vs-prior delta chip.
- **Insights** — 2×2 chart-card grid (1-col <1024px): yield/acre by season (grouped bars per crop),
  cost vs harvest value (two lines), crop mix (donut), field profitability (horizontal bars, values
  as mono labels at bar ends). Top-right: "Export CSV" secondary button — locked on Free (below).
- **Season report** (Pro) — printable: white full-bleed page, `max-w-[800px]` centered, Fraunces
  serif headings, sections (summary stats, per-field table, activity log) separated by 1px rules;
  header shows org name, season, generated date (mono). `@media print`: hide shell/nav/buttons,
  black-on-white, `break-inside: avoid` on tables. On-screen top bar: "Print / Save PDF" primary.
- **Members** — table: avatar+name, email (mono 13px), role badge, joined (mono date). Owners get a
  role select per row and an "Invite member" primary + drawer. Non-owners see a read-only table
  (no empty select stubs — omit the column's controls entirely).
- **Plan & billing** — left card: current plan (large plan badge, Fraunces plan name, entitlement
  list with check icons in brand-600 / lock icons in ink-faint for Free's disabled rows). **Usage
  meter**: "Active fields — 2 of 3" mono label + 8px progress bar (radius full, sunken track,
  brand-600 fill; fill turns `--color-warn` at 3/3). Owner sees primary "Upgrade to Pro" (or
  secondary "Downgrade" with confirm modal; surface the 422 archive-fields error as an inline
  warn-bg callout, not a toast). Right card: plan-change history list (mono dates, "Free → Pro",
  changed-by name). Non-owners see the cards without buttons plus a 13px ink-soft note: "Only
  owners can change the plan."
- **Locked/upsell states (Free)** — one shared pattern, used for Season report, CSV export, and the
  4th field: keep the feature visible, never hide it. Card/section shows a 16px lock icon +
  overline "PRO", one factual sentence ("Season reports are included in Pro."), and — owners only —
  a sm primary "Upgrade to Pro" linking to billing; non-owners get "Ask an owner to upgrade" in
  ink-soft. Locked buttons render as secondary with a lock icon, `aria-disabled`, opening a small
  popover with that same copy. ContourPattern at 4% behind upsell cards. No countdowns, no fake
  discounts, no red, no nagging modals.
- **Role-driven visibility** — viewers never see create/edit/delete controls (omit, don't disable);
  agronomists see field/activity editing but not member-role or plan controls.

## 7. Accessibility

- Contrast: body text pairs above meet AA on their backgrounds; never set ink-faint text smaller
  than 12px; badge text colors must hit 4.5:1 against their tinted backgrounds (darken accents as
  specified, don't use raw `--color-act-*` for text).
- Focus: global `focus-visible` = 2px solid brand-600 ring, 2px offset, on every interactive element
  including table rows and chart-card export buttons. Never `outline: none` without replacement.
- Keyboard: sidebar nav is plain links in a `<nav aria-label="Primary">`; org switcher and user menu
  use the menu-button pattern (`aria-haspopup="menu"`, `aria-expanded`, arrow-key traversal, Escape
  closes); drawer/modal focus trap per §4.6; tabs use `role="tablist"` with arrow keys.
- Announce: toasts per §4.7; form errors focus the first invalid field on submit; charts get an
  `aria-label` summarizing the takeaway and a visually-hidden data table alternative is a plus, not
  required.
- Color is never the only signal: activity badges have text labels, deltas have ▲/▼ + sign, chart
  legends always render.

## 8. Do not

- No dark mode, no purple-gradient "AI SaaS" look, no glassmorphism/neumorphism, no hero glows.
- No external assets at runtime: no font/CDN links, no remote images, no icon CDNs — fonts via
  `next/font`, icons via `lucide-react` imports only.
- No stock illustrations or emoji as UI; the only decoration is the ContourPattern SVG.
- No pure cool grays (`#F9FAFB` etc.) — every neutral comes from the warm token set above.
- No spinners for page loads (skeletons only); no zebra tables; no centered page titles; no
  ALL-CAPS buttons; no more than one primary button per view region.
- No dark-pattern upsells: no hiding features, no disabled-looking traps without explanation,
  no urgency copy.
- Numbers never in the sans face — if it's a quantity, date, or currency, it's Spline Sans Mono
  with tabular-nums.
