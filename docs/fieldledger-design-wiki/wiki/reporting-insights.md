---
title: Reporting and Insights
status: proposed
tags: [fieldledger, reporting, analytics, pdf]
last_updated: 2026-07-09
related: [billing-entitlements, api-design, frontend-design]
---

# Reporting and Insights

Reporting is the public echo of the gated agricultural PDF-report work. The goal is to show an end-to-end path from operational data to compiled business output.

## Dashboard metrics

| Metric | Definition |
|---|---|
| Active fields | Count of unarchived fields in org. |
| Total acreage | Sum of active field acreage. |
| Season progress | Current date relative to selected season start/end. |
| Recent activity | Latest activity records across fields. |
| Input cost | Sum of non-harvest cost activity amounts. |
| Harvest value | Sum of harvest revenue amounts. |
| Net value | Harvest value minus input cost. |
| Yield per acre | Harvest quantity divided by acreage. |

## Insights page

Charts:

1. Yield per acre by season.
2. Input cost vs harvest value by season.
3. Crop mix by acreage.
4. Field-level net value.
5. Activity count by type.

## Query shape

`GET /api/orgs/{orgId}/insights`

```json
{
  "selectedSeasonId": "uuid",
  "totals": {
    "activeFields": 3,
    "totalAcreage": 220.5,
    "inputCost": 68350.42,
    "harvestValue": 142109.88,
    "netValue": 73759.46
  },
  "yieldBySeason": [
    { "year": 2024, "crop": "corn", "yieldPerAcre": 184.2 }
  ],
  "costVsValue": [
    { "year": 2024, "inputCost": 52200.00, "harvestValue": 118000.00 }
  ]
}
```

## Season report sections

1. Cover:
   - organization name,
   - season year,
   - generated timestamp,
   - plan badge.

2. Executive summary:
   - active fields,
   - total acreage,
   - total input cost,
   - total harvest value,
   - net value,
   - average yield per acre.

3. Field breakdown:
   - field name,
   - crop,
   - acreage,
   - activity count,
   - cost,
   - revenue,
   - yield per acre.

4. Activity summary:
   - planting,
   - spraying,
   - irrigation,
   - fertilizer,
   - harvest.

5. Charts/tables:
   - yield per acre by field,
   - cost vs harvest value,
   - crop mix.

## PDF generation

Recommended v1 approach:

- Generate report server-side in ASP.NET Core.
- Start with HTML preview for fast iteration.
- Add PDF output after report data contract stabilizes.
- QuestPDF is a reasonable C# candidate; verify license constraints before production use.

Relevant docs:

- [QuestPDF quick start](../docs/source-map.md#reporting)

## Export behavior

CSV endpoint:

```http
GET /api/orgs/{orgId}/exports/activities.csv?seasonId={seasonId}
```

Rules:

- Pro required.
- Owner or agronomist required.
- Viewer cannot export CSV in v1.
- CSV generation must stream from server-side query output.

Example columns:

```text
organization,field,season,crop,activity_type,activity_date,quantity,quantity_unit,cost_amount,revenue_amount,notes
```

## Report gating

The frontend should show upsells for Free orgs, but all report/export endpoints must enforce entitlement server-side. Gating reads the org's `entitlements` row, which changes only through the in-app, owner-only plan action (`app.set_org_plan`); see [[billing-entitlements]]. When the owner upgrades, report and CSV access flips immediately on the next request.
