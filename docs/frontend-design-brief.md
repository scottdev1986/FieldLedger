# FieldLedger — Frontend Design Brief

Prescriptive spec for the FieldLedger web UI (Next.js App Router, Tailwind CSS v4, TypeScript, React).
Implement exactly as written; where a value is given, use that value.

---

## 1. Design direction: "Meridian" — modern SaaS polish

FieldLedger looks like a venture-backed SaaS product's app in the vein of Linear, Stripe, and
Vercel dashboards: a crisp neutral canvas, **one** confident green accent, tight consistent
spacing, subtle depth from hairline borders and soft cool shadows, refined data tables, and
tabular-mono figures. It is a farm-operations product — the green accent is the only nod to
agriculture. It is a **light-theme-only** application. No dark mode in v1.

Personality words: *precise, contemporary, confident, data-forward*. The one memorable thing:
every figure (acreage, currency, yield, dates) is set in Geist Mono with tabular numerals against
an otherwise ruthlessly neutral zinc UI, so numbers pop the way they do in Stripe.

Hard rules that define the look:

- Near-white zinc canvas (`#FAFAFA`), pure white cards, `#E4E4E7` hairline borders. Depth comes
  from 1px borders + soft layered shadows, never heavy elevation, never glassmorphism/blur panels.
- Green is used sparingly and deliberately: primary buttons, links, active nav, focus rings, the
  logo mark, and the lead chart series. Large green surfaces are forbidden everywhere.
- No texture, no paper tones, no serif faces, no illustration. The only decorative surfaces are
  the auth page's dark left panel (radial green glow + 24px dot grid at 5–6% white) — nothing else.
- All metrics, dates, acreages, currency, and axis labels use Geist Mono with
  `font-variant-numeric: tabular-nums`.
- Headings are Geist semibold with `tracking-tight`. Section labels are 11px/600 uppercase
  "overlines" (see 3.2).

## 2. Design tokens (Tailwind v4 `@theme` in `app/globals.css`)

Fonts: load with `next/font/google` (self-hosted at build time — zero runtime network requests):
**Geist** (all UI text and headings, weights via variable font) and **Geist Mono** (numerals,
dates, currency, axis labels). Expose via CSS variables in the root layout (`--font-geist-sans`,
`--font-geist-mono`). No `@import` of fonts, no `fonts.googleapis.com` anywhere.

```css
@theme {
  /* Brand — single green accent (AA-safe anchors for actions/links) */
  --color-brand-50:  #F0FDF4;
  --color-brand-100: #DCFCE7;
  --color-brand-200: #BBF7D0;
  --color-brand-300: #86EFAC;
  --color-brand-400: #4ADE80;
  --color-brand-500: #16A34A;  /* focus rings, icons, chart lead, logo mark */
  --color-brand-600: #15803D;  /* primary buttons (white text = 5.0:1) */
  --color-brand-700: #166534;  /* links, active nav text, hovers */
  --color-brand-800: #14532D;
  --color-brand-900: #052E16;

  /* Neutrals — crisp zinc scale */
  --color-paper:      #FAFAFA;  /* app + sidebar background */
  --color-surface:    #FFFFFF;  /* cards, tables, modals, topbar */
  --color-sunken:     #F4F4F5;  /* wells, hover fills, skeleton base */
  --color-line:       #E4E4E7;  /* hairline borders */
  --color-line-strong:#D4D4D8;  /* input borders, emphasized dividers */
  --color-ink:        #18181B;  /* primary text */
  --color-ink-soft:   #52525B;  /* secondary text */
  --color-ink-faint:  #71717A;  /* placeholders, tertiary (never < 12px) */

  /* Semantic */
  --color-success: #15803D;  --color-success-bg: #F0FDF4;
  --color-warn:    #B45309;  --color-warn-bg:    #FFFBEB;
  --color-danger:  #DC2626;  --color-danger-bg:  #FEF2F2;
  --color-info:    #0369A1;  --color-info-bg:    #F0F9FF;

  /* Chart categorical (series order; green always leads) */
  --color-chart-1: #16A34A;  /* green (accent) */
  --color-chart-2: #6366F1;  /* indigo */
  --color-chart-3: #0EA5E9;  /* sky */
  --color-chart-4: #F59E0B;  /* amber */
  --color-chart-5: #EC4899;  /* pink */
  --color-chart-6: #14B8A6;  /* teal */

  /* Activity-type accents (badges, timeline dots) */
  --color-act-planting:   #16A34A;
  --color-act-spraying:   #8B5CF6;
  --color-act-irrigation: #0EA5E9;
  --color-act-fertilizer: #D97706;
  --color-act-harvest:    #F59E0B;
  --color-act-note:       #71717A;

  /* Type */
  --font-sans: var(--font-geist-sans), ui-sans-serif, system-ui, sans-serif;
  --font-mono: var(--font-geist-mono), ui-monospace, monospace;

  /* Radii */
  --radius-sm: 6px;   /* chips, small controls */
  --radius-md: 8px;   /* buttons, inputs, menus */
  --radius-lg: 12px;  /* cards, modals, drawers */

  /* Shadows (cool, layered, faint) */
  --shadow-card:  0 1px 2px rgb(24 24 27 / 0.04), 0 1px 3px rgb(24 24 27 / 0.04);
  --shadow-pop:   0 4px 6px -1px rgb(24 24 27 / 0.08), 0 2px 4px -2px rgb(24 24 27 / 0.06);
  --shadow-modal: 0 20px 25px -5px rgb(24 24 27 / 0.12), 0 8px 10px -6px rgb(24 24 27 / 0.08);
}
```

Badge text darkened to AA against tinted backgrounds (component CSS, not theme tokens):
planting `#166534`, spraying `#6D28D9`, irrigation `#0369A1`, fertilizer `#92400E`,
harvest `#B45309`, note `#52525B`.

Spacing rhythm: 4px base grid. Card padding `p-5` (20px). Gap between cards `gap-4` (16px).
Page gutter `px-6` desktop / `px-4` mobile. Vertical section spacing `space-y-6` (24px).
Content max width `max-w-[1200px] mx-auto`. Body text 14px/1.5; secondary 13px; tables 13px.
Transitions: color/shadow only, 150ms ease; no scale transforms that shift layout.

## 3. Layout system

### 3.1 App shell (`app/orgs/[orgId]/layout.tsx`)
- **Sidebar**: fixed left, `w-[260px]`, background `--color-paper`, right border `--color-line`.
  Top→bottom: (1) logo — a 24px `radius-md` brand-600 square with a white 16px lucide `sprout`
  glyph, wordmark "FieldLedger" 15px/600 tracking-tight; (2) **org switcher** — full-width button
  (surface bg, line-strong border, shadow-xs) showing org name + chevron, opens a popover menu
  (shadow-pop, radius-lg) listing the user's orgs with the **plan badge** (4.3) next to each and an
  "All organizations" link to `/orgs`; (3) nav items: Dashboard, Fields, Insights, Season report,
  Members, Plan & billing. Nav item: 36px tall, radius-md, 13px/500, ink-soft with 16px lucide
  icon; active = `bg-brand-50 text-brand-700` (a quiet filled pill — no left bars, no underlines);
  hover = `bg-sunken text-ink`. "Season report" shows a right-aligned lock icon (14px, ink-faint)
  when the org is Free. (4) footer pinned bottom: plan badge + "Upgrade" text link (Free, owners).
- **Topbar**: 56px, solid surface background (no blur), bottom border `--color-line`. Left: page
  breadcrumb (org name / page, 13px ink-soft). Right: user menu button (28px circle avatar with
  initials on brand-100/brand-800, plus display name at ≥1024px) opening a menu: email
  (non-interactive), role in current org, Sign out.
- **Responsive**: below 1024px the sidebar becomes an overlay drawer (same content) toggled by a
  hamburger in the topbar; page content is single-column. Card grids: 4-col ≥1280px, 2-col ≥768px,
  1-col below.

### 3.2 Page header pattern
Every page: overline label (11px, uppercase, weight 600, tracking `0.06em`, ink-faint) naming the
org, then the page title at 24px/600 `tracking-tight` ink, optional one-line description (14px
ink-soft), and a right-aligned primary action (e.g. "Add field"). 24px margin below. Never center
titles.

### 3.3 Cards, tables, empty states, skeletons
- **Card**: surface bg, 1px `--color-line` border, radius-lg, shadow-card, p-5. Card title =
  overline style (3.2) — not bold 16px text.
- **Table**: inside a card with `p-0`; header row has **no fill** — 12px/500 sentence-case
  ink-soft labels on a 1px `--color-line` bottom border (Stripe-style, no uppercase shouting);
  body rows 44px, 1px `--color-line` row dividers, no zebra; numeric columns right-aligned in
  mono; row hover `bg-paper` with a 150ms transition; first column is the entity name at 500
  weight, clickable, brand-700 on hover. Overflow-x scroll on mobile.
- **Empty state**: centered in the card — a 44px `radius-lg` sunken tile with a 1px line border
  holding a 20px lucide icon in ink-faint, then a 15px/600 title, a 13px ink-soft line, and a
  primary button when the viewer's role can create. No background decoration.
- **Skeletons**: match final layout exactly (same card/table shells); blocks are `bg-sunken`
  radius-sm with `animate-pulse`. Never spinners for page loads; spinners (16px) only inside
  buttons during submits.

## 4. Components

### 4.1 Buttons
Sizes: sm 32px / md 40px (default) / lg 44px height; px 12/16/20; 13/14/14px text at 500;
radius-md; 150ms color transitions.
- **primary**: bg brand-600, white text, `shadow-xs`; hover brand-700; active brand-800; disabled
  50% opacity + `cursor-not-allowed`.
- **secondary**: surface bg, 1px line-strong border, ink text, `shadow-xs`; hover bg-sunken.
- **ghost**: transparent, ink-soft; hover bg-sunken + ink text.
- **destructive**: bg `--color-danger`, white, `shadow-xs`; hover darken 8–10%.
All: focus-visible ring per §7; loading state shows a 16px spinner beside the label.

### 4.2 Forms (React Hook Form + Zod)
Label 13px/500 ink, 6px above input. Input: 40px, surface bg, 1px line-strong border, radius-md,
subtle `0 1px 2px rgb(24 24 27 / 0.03)` inset-feel shadow, 14px text, placeholder ink-faint;
focus = brand-500 border + 3px ring at 18% brand-500. Error state: danger border + 3px ring at
14% danger, message below in 12px danger with a 14px `circle-alert` icon; add `aria-invalid` and
`aria-describedby` pointing at the message id. Help text 12px ink-faint. Selects/dates match
input styling. Mark optional fields with "(optional)" in ink-faint — no asterisks.

### 4.3 Badges
Base: 22px tall pill (radius full), 11px/500, px-2, 1px border where noted.
- **Plan**: Free = surface bg / line-strong border / ink-soft text, uppercase tracking-wide.
  Pro = solid ink (`#18181B`) bg / white text, no border, uppercase tracking-wide.
- **Role** (uppercase tracking-wide): owner = warn-bg/warn; agronomist = brand-50/brand-700;
  viewer = info-bg/info.
- **Activity type** (normal case): tinted pill using the `--color-act-*` accents — text at the
  darkened AA color listed in §2, bg = `color-mix(in srgb, var(--color-act-x) 10%, white)`,
  border at 28%. Each includes a 6px filled dot in the raw accent color before the label.

### 4.4 Stat cards (dashboard)
Card per §3.3 with: overline metric name; value in Geist Mono 26px/600 tracking-tight ink with
tabular-nums (units like "ac" or "%" at 16px/400 ink-soft); below, a 12px ink-soft context line
("across 4 fields") or a delta chip (mono 11px, success/danger tint, ▲/▼ glyph + sign). No
sparklines in v1. Height locks to content; grid per §3.1.

### 4.5 Activity timeline
Vertical list: 1px `--color-line` rail on the left; each item = a 10px dot in the activity's
accent color (2px paper ring) on the rail, then: row 1 = activity-type badge + title (14px/500),
row 2 = 13px ink-soft detail, row 3 = mono 12px ink-faint date + acreage. 16px gap between items.
Group under sticky overline month headers ("APRIL 2026"). Editable rows (owner/agronomist) show a
ghost kebab menu on hover/focus with Edit/Delete.

### 4.6 Modals & drawers
Create/edit forms open in a **right-side drawer**: `w-[440px]` (full-width sheet under 640px),
surface bg, shadow-modal, radius-lg on the left corners, header (16px/600 tracking-tight title +
ghost X), scrollable body, pinned footer with secondary Cancel + primary submit. Destructive
confirms use a centered **modal** `max-w-[400px]`. Overlay: `rgb(9 9 11 / 0.4)`. Both: focus
trap, Escape closes, focus returns to trigger, `role="dialog"` + `aria-labelledby`. Animate 160ms
ease-out slide/fade; respect `prefers-reduced-motion`.

### 4.7 Toasts
Bottom-right stack, max 3. Surface bg, 1px line border, radius-lg, shadow-pop, 3px left accent
bar (success/danger/info), 14px title + optional 13px ink-soft line, auto-dismiss 5s (errors
persist until dismissed), `role="status"` (errors `role="alert"`). No color-filled toasts.

## 5. Recharts rules

- Series colors: `--color-chart-1..6` in order; single-series charts use chart-1 (green) only.
  In the cost-vs-value line pair, "Harvest value" takes green (the positive series) and "Input
  cost" takes indigo.
- Grid: horizontal `CartesianGrid` lines only, stroke `#E4E4E7`, no vertical lines, no dashes.
- Axes: no axis lines, no tick lines; tick labels 11px Geist Mono `#71717A`; Y-axis ticks
  formatted with units ("180 bu", "$12k"); ≤5 Y ticks.
- Bars: radius `[4,4,0,0]` (`[0,4,4,0]` horizontal), max bar size 40, category gap ≥ 24%.
  Lines: 2px, dot r=3 only on hover/active. Pie (crop mix): donut, inner radius 60%, 2px white
  stroke between slices, legend right (bottom on mobile) — never labels-on-slices.
- Legends: circle icons (`iconType="circle"`, size 8), 12px text.
- Tooltip: custom component — surface bg, 1px line border, radius-md, shadow-pop, 12px; label row
  500-weight sans, series rows = 8px color dot + name + right-aligned mono value. Cursor:
  `fill: #F4F4F5` for bars, 1px `#D4D4D8` line for lines.
- `isAnimationActive={false}` on **every** series (Bar/Line/Pie) — required for React 19; do not
  remove.
- Every chart sits in a card with an overline title + 12px ink-soft subtitle; fixed heights:
  280px standard, 220px compact.

## 6. Page-by-page

- **/login** — split screen. Left 44% (hidden <900px): `zinc-950` background with two soft
  radial brand-glows (bottom-left `rgb(22 163 74 / 0.22)`, top-right at 0.10) and a 24px dot grid
  (`rgb(255 255 255 / 0.05)` 1px dots at 60% layer opacity); wordmark top-left (inverse), one
  32px/600 tracking-tight white statement: "The record book for every acre." plus a 14px zinc-400
  subline; 12px zinc-500 footer line. Right 56%: paper bg, centered `max-w-[380px]` form — email,
  password, primary "Sign in" (full width). Below a hairline divider labeled "Demo accounts"
  (overline, centered): three full-width secondary buttons, each with the role badge and label
  ("Sign in as Owner" etc.; one click = login with seeded creds). Footer microcopy 12px ink-faint.
- **/orgs** — no sidebar; slim topbar (wordmark + user menu). Page header "Your organizations";
  grid of org cards (2-col ≥768px): org name 18px/600 tracking-tight, role + plan badge row, mono
  meta line ("4 fields · 3 seasons"); whole card clickable — hover = border line-strong +
  shadow-pop (150ms), chevron nudges right.
- **/orgs/[orgId]** (dashboard) — header per §3.2. Stat-card grid (§4.4). Below, two-column
  (2fr/1fr): "Recent activity" card (timeline §4.5, last 8, "View fields" link) and a "This
  season" card (season name 18px/600, crop list with chart-color dots, mono days-to-harvest),
  plus the plan/limit card (warn tint when a Free org hits 3 fields).
- **Fields list** — header + primary "Add field" (owner/agronomist; on Free at 3 fields use the
  locked pattern below). Table: Name, Crop, Acreage (mono, right), Last activity (badge + mono
  date), Status. Row click → detail. Empty state §3.3.
- **Field detail** — header: field name + mono acreage; meta strip card of facts (status badge,
  soil, mono planted date, season select). Tabs (underline style, 2px brand-600 active
  indicator, arrow-key switchable): Activity / Seasons. Activity = timeline §4.5 + "Log activity"
  primary opening the drawer. Seasons = table of per-season yield with vs-prior delta in
  success/danger + ▲/▼.
- **Insights** — 2×2 chart-card grid (1-col <1024px) per §5. Top-right: "Export CSV" secondary —
  locked on Free (below).
- **Season report** (Pro) — printable: white page card, `max-w-[800px]` centered, 24–30px
  semibold tracking-tight headings, sections (summary stats, per-field table, activity summary
  tiles, activity log) separated by 1px rules; header shows org name, season, mono generated
  date. `@media print`: hide shell/nav/buttons, black-on-white, `break-inside: avoid` on tables.
  On-screen top bar: secondary "Export CSV" + primary "Print / Save PDF".
- **Members** — table: avatar+name, email (mono 13px), role (owners get a select; others see the
  badge), joined (mono date). Owners get "Invite member" primary + drawer. Non-owners see a
  read-only table and a 13px note.
- **Plan & billing** — left card: current plan (plan badge, 24px/600 tracking-tight plan name,
  entitlement list with brand-600 check icons / ink-faint locks). **Usage meter**: mono "Active
  fields — 2 of 3" label + 8px full-radius progress bar (sunken track, brand-600 fill; fill turns
  `--color-warn` at limit). Owner sees primary "Upgrade to Pro" (or secondary "Downgrade" with
  confirm modal; surface the 422 archive-fields error as an inline warn-bg callout, not a toast).
  Right card: plan-change history (mono dates, "Free → Pro", changed-by). Non-owners see the
  cards without buttons plus "Only owners can change the plan."
- **Locked/upsell states (Free)** — one shared pattern for Season report, CSV export, and the 4th
  field: keep the feature visible, never hide it. Card/section shows a 16px lock icon + overline
  "PRO" in brand-700, one factual sentence, and — owners only — a sm primary "Upgrade to Pro"
  linking to billing; non-owners get "Ask an owner to upgrade" in ink-soft. Locked buttons render
  as secondary with a lock icon, `aria-disabled`, opening a small popover with the same copy.
  No countdowns, no fake discounts, no red, no nagging modals.
- **Role-driven visibility** — viewers never see create/edit/delete controls (omit, don't
  disable); agronomists edit fields/activities but not member roles or plan.

## 7. Accessibility

- Contrast: all text pairs meet 4.5:1 on their backgrounds (ink-faint `#71717A` on white = 4.7:1
  — never set it smaller than 12px); primary buttons use brand-600 `#15803D` so white text passes
  AA; badge text uses the darkened accent colors from §2, never the raw `--color-act-*` values.
- Focus: global `focus-visible` = 2px solid brand-500 ring, 2px offset, on every interactive
  element. Never `outline: none` without replacement.
- Keyboard: sidebar nav is plain links in `<nav aria-label="Primary">`; org switcher and user
  menu use the menu-button pattern (`aria-haspopup="menu"`, `aria-expanded`, arrow-key traversal,
  Escape closes); drawer/modal focus trap per §4.6; tabs use `role="tablist"` with arrow keys.
- Announce: toasts per §4.7; form errors wire `aria-invalid`/`aria-describedby`; charts carry an
  `aria-label` summarizing the takeaway.
- Color is never the only signal: activity badges have text labels, deltas have ▲/▼ + sign,
  chart legends always render.
- Respect `prefers-reduced-motion` globally (animations collapse to ~0ms).

## 8. Do not

- No dark mode, no purple-gradient "AI SaaS" look, no glassmorphism, no neumorphism, no
  backdrop-blur panels, no hero glows inside the app shell (the auth panel's radial glow is the
  single exception).
- No rustic/paper/texture styling: no warm beige neutrals, no serif faces, no topographic or
  organic patterns, no stock illustration, no emoji as UI.
- No external assets at runtime: fonts via `next/font/google` only, icons via `lucide-react`
  imports only.
- No second accent color in UI chrome — semantic colors and chart hues are the only non-green
  chroma, and they never style buttons, nav, or links.
- No spinners for page loads (skeletons only); no zebra tables; no uppercase table headers; no
  centered page titles; no ALL-CAPS buttons; no more than one primary button per view region.
- No scale-on-hover transforms; hover feedback is color/border/shadow only.
- No dark-pattern upsells: no hiding features, no disabled-looking traps without explanation, no
  urgency copy.
- Numbers never in the sans face — if it's a quantity, date, or currency, it's Geist Mono with
  tabular-nums.
- Never remove `isAnimationActive={false}` from a Recharts series.
