"use client";

import { useQuery } from "@tanstack/react-query";
import { CalendarPlus, ChevronRight, Gauge, Sprout } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { ActivityTimeline } from "@/features/activities/ActivityTimeline";
import { StatCard } from "@/features/orgs/StatCard";
import { SeasonDrawer } from "@/features/seasons/SeasonDrawer";
import { PlanBadge } from "@/components/ui/Badge";
import { Button, buttonClasses } from "@/components/ui/Button";
import { Card, EmptyState, ErrorState, Overline, PageHeader } from "@/components/ui/Primitives";
import { AppSkeleton } from "@/components/ui/Skeleton";
import { useCurrentOrg } from "@/features/orgs/OrgContext";
import { apiFetch } from "@/lib/api-client";
import type { DashboardResponse } from "@/lib/api-types";
import { cx, formatCurrency, formatNumber } from "@/lib/format";
import { queryKeys } from "@/lib/query-keys";

export default function DashboardPage() {
  const org = useCurrentOrg();
  const [seasonOpen, setSeasonOpen] = useState(false);
  const query = useQuery({ queryKey: queryKeys.dashboard(org.id), queryFn: () => apiFetch<DashboardResponse>(`/api/orgs/${org.id}/dashboard`) });
  if (query.isLoading) return <AppSkeleton />;
  if (query.error || !query.data) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  const { metrics, currentSeason, recentActivities, cropProgress, limits } = query.data;
  const canEdit = org.role !== "viewer";
  const metricCards = [
    { label: "Active fields", value: formatNumber(metrics.activeFieldCount, 0), context: limits.maxFields ? `of ${limits.maxFields} on Free` : "no field limit" },
    { label: "Total acreage", value: formatNumber(metrics.totalAcreage), unit: "ac", context: `across ${metrics.activeFieldCount} active fields` },
    { label: "Season progress", value: metrics.seasonProgressPercent === null ? "—" : formatNumber(metrics.seasonProgressPercent, 0), unit: metrics.seasonProgressPercent === null ? undefined : "%", context: currentSeason?.name ?? "No current season" },
    { label: "Activities this season", value: formatNumber(metrics.activitiesThisSeason, 0), context: currentSeason?.name ?? "No current season" },
    { label: "Input cost", value: formatCurrency(metrics.inputCost, true), context: "current season" },
    { label: "Harvest value", value: formatCurrency(metrics.harvestValue, true), context: "estimated from harvest logs" },
    { label: "Net value", value: formatCurrency(metrics.netValue, true), context: "harvest value less inputs" },
    { label: "Yield per acre", value: metrics.yieldPerAcre === null ? "—" : formatNumber(metrics.yieldPerAcre), unit: metrics.yieldPerAcre === null ? undefined : "bu/ac", context: "harvest quantity ÷ acreage" },
  ];
  return <>
    <PageHeader action={canEdit ? <Button onClick={() => setSeasonOpen(true)} variant="secondary"><CalendarPlus className="h-4 w-4" />Create season</Button> : undefined} description="Seasonal operations and farm value at a glance." overline={org.name} title={org.name} />
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">{metricCards.map((metric) => <StatCard key={metric.label} {...metric} />)}</div>
    <div className="mt-6 grid gap-4 lg:grid-cols-[2fr_1fr]">
      <Card>
        <div className="mb-5 flex items-center justify-between"><div><Overline>Recent activity</Overline><p className="mt-2 text-xs text-ink-soft">Latest field records across the organization.</p></div><Link className="text-[13px] font-medium text-brand-700 hover:underline" href={`/orgs/${org.id}/fields`}>View fields</Link></div>
        {recentActivities.length ? <ActivityTimeline activities={recentActivities.slice(0, 8)} /> : <EmptyState description="Activity records will appear here as work is logged." icon={Sprout} title="No activity yet" />}
      </Card>
      <div className="space-y-4">
        <Card>
          <Overline>This season</Overline>
          {currentSeason ? <><h2 className="mt-3 text-lg font-semibold tracking-tight">{currentSeason.name}</h2><div className="mt-5 space-y-4">{cropProgress.map((crop, index) => <div className="flex items-start gap-3" key={crop.crop}><span aria-hidden="true" className="mt-1.5 h-2 w-2 rounded-full" style={{ backgroundColor: `var(--color-chart-${(index % 6) + 1})` }} /><div className="min-w-0 flex-1"><div className="flex justify-between gap-3"><p className="font-medium">{crop.crop}</p><p className="font-mono text-xs text-ink-faint">{formatNumber(crop.acreage)} ac</p></div><p className="mt-1 text-xs text-ink-soft"><span className="font-mono">{crop.fieldCount}</span> {crop.fieldCount === 1 ? "field" : "fields"}{crop.daysToHarvest !== null ? <span> · <span className="font-mono">{crop.daysToHarvest}</span> days to harvest</span> : null}</p></div></div>)}</div></> : <div className="py-8 text-center"><Gauge className="mx-auto h-7 w-7 text-ink-faint" /><p className="mt-3 text-[15px] font-semibold">No current season</p>{canEdit ? <Button className="mt-4" onClick={() => setSeasonOpen(true)} size="sm">Create season</Button> : null}</div>}
        </Card>
        <Card className={cx(org.plan === "free" && metrics.activeFieldCount >= 3 && "border-warn/30 bg-warn-bg/40")}>
          <div className="flex items-center justify-between"><Overline>Plan & field limit</Overline><PlanBadge plan={org.plan} /></div>
          <p className="mt-3 text-sm text-ink">{org.plan === "free" ? `${metrics.activeFieldCount} of ${limits.maxFields ?? 3} active fields used.` : "Unlimited active fields and reporting exports are enabled."}</p>
          {org.role === "owner" ? <Link className={cx(buttonClasses("secondary", "sm"), "mt-4")} href={`/orgs/${org.id}/billing`}>Manage plan<ChevronRight className="h-3.5 w-3.5" /></Link> : null}
        </Card>
      </div>
    </div>
    {seasonOpen ? <SeasonDrawer onClose={() => setSeasonOpen(false)} orgId={org.id} /> : null}
  </>;
}
