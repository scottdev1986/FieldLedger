"use client";

import { useQuery } from "@tanstack/react-query";
import { Download, Printer } from "lucide-react";
import { useParams } from "next/navigation";
import { useState } from "react";
import { ActivityBadge, PlanBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { ErrorState, Overline, PageHeader, ProLock } from "@/components/ui/Primitives";
import { AppSkeleton } from "@/components/ui/Skeleton";
import { useToast } from "@/components/ui/Toast";
import { useCurrentOrg } from "@/features/orgs/OrgContext";
import { apiFetch, apiFetchBlob } from "@/lib/api-client";
import type { ReportResponse } from "@/lib/api-types";
import { formatCurrency, formatDate, formatNumber, titleCase } from "@/lib/format";
import { queryKeys } from "@/lib/query-keys";

export default function SeasonReportPage() {
  const org = useCurrentOrg(); const { seasonId } = useParams<{ seasonId: string }>(); const { toast } = useToast(); const [exporting, setExporting] = useState(false);
  const query = useQuery({ queryKey: queryKeys.report(org.id, seasonId), queryFn: () => apiFetch<ReportResponse>(`/api/orgs/${org.id}/seasons/${seasonId}/report`), enabled: org.plan === "pro" });
  const download = async () => { setExporting(true); try { const blob = await apiFetchBlob(`/api/orgs/${org.id}/exports/activities.csv?seasonId=${encodeURIComponent(seasonId)}`); const url = URL.createObjectURL(blob); const anchor = document.createElement("a"); anchor.href = url; anchor.download = `fieldledger-${seasonId}-activities.csv`; anchor.click(); URL.revokeObjectURL(url); } catch (error) { toast({ type: "danger", title: "Export failed", description: error instanceof Error ? error.message : undefined }); } finally { setExporting(false); } };
  if (org.plan === "free") return <><PageHeader description="A printable record of seasonal performance and operations." overline={org.name} title="Season report" /><ProLock orgId={org.id} role={org.role} sentence="Season reports are included in Pro." /></>;
  if (query.isLoading) return <AppSkeleton />;
  if (query.error || !query.data) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  const report = query.data;
  return <>
    <div className="no-print mb-5 flex flex-wrap items-end justify-between gap-4"><div><p className="overline">{org.name}</p><h1 className="mt-2 font-display text-[28px] font-semibold">Season report</h1></div><div className="flex gap-2">{org.role !== "viewer" ? <Button loading={exporting} onClick={() => void download()} variant="secondary"><Download className="h-4 w-4" />Export CSV</Button> : null}<Button onClick={() => window.print()}><Printer className="h-4 w-4" />Print / Save PDF</Button></div></div>
    <article className="report-page mx-auto max-w-[800px] border border-line bg-white p-8 shadow-card sm:p-12">
      <header className="border-b border-line pb-8"><div className="flex items-start justify-between gap-4"><div><Overline>FieldLedger season record</Overline><h2 className="mt-3 font-display text-3xl font-semibold">{report.organization.name}</h2><p className="mt-2 font-display text-xl text-ink-soft">{report.season.name}</p></div><PlanBadge plan={report.organization.plan} /></div><p className="mt-7 font-mono text-xs text-ink-faint">Generated {formatDate(report.generatedAt, { month: "long", day: "numeric", year: "numeric", hour: "numeric", minute: "2-digit" })}</p></header>
      <section className="report-section border-b border-line py-8"><Overline>Executive summary</Overline><div className="mt-5 grid grid-cols-2 gap-x-8 gap-y-6 sm:grid-cols-3">{[
        ["Active fields", formatNumber(report.summary.activeFields, 0)], ["Total acreage", `${formatNumber(report.summary.totalAcreage)} ac`], ["Input cost", formatCurrency(report.summary.inputCost)], ["Harvest value", formatCurrency(report.summary.harvestValue)], ["Net value", formatCurrency(report.summary.netValue)], ["Average yield", report.summary.averageYieldPerAcre === null ? "—" : `${formatNumber(report.summary.averageYieldPerAcre)} bu/ac`],
      ].map(([label, value]) => <div key={label}><Overline>{label}</Overline><p className="mt-2 font-mono text-lg font-medium">{value}</p></div>)}</div></section>
      <section className="report-section border-b border-line py-8"><Overline>Field breakdown</Overline><div className="mt-4 overflow-x-auto"><table className="ledger-table"><thead><tr><th>Field / crop</th><th className="numeric">Acres</th><th className="numeric">Activities</th><th className="numeric">Cost</th><th className="numeric">Value</th><th className="numeric">Yield/ac</th></tr></thead><tbody>{report.fields.map((field) => <tr key={field.fieldId}><td><p className="font-medium">{field.fieldName}</p><p className="text-xs text-ink-soft">{field.crop}</p></td><td className="numeric">{formatNumber(field.acreage)}</td><td className="numeric">{field.activityCount}</td><td className="numeric">{formatCurrency(field.inputCost)}</td><td className="numeric">{formatCurrency(field.harvestValue)}</td><td className="numeric">{field.yieldPerAcre === null ? "—" : formatNumber(field.yieldPerAcre)}</td></tr>)}</tbody></table></div></section>
      <section className="report-section border-b border-line py-8"><Overline>Activity summary</Overline><div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-3">{report.activitySummary.map((item) => <div className="rounded-md border border-line p-3" key={item.type}><ActivityBadge type={item.type} /><p className="mt-2 font-mono text-xl">{item.count}</p></div>)}</div></section>
      <section className="report-section pt-8"><Overline>Activity log</Overline><div className="mt-4 overflow-x-auto"><table className="ledger-table"><thead><tr><th>Date</th><th>Field</th><th>Type</th><th className="numeric">Cost</th><th className="numeric">Revenue</th></tr></thead><tbody>{report.activities.map((activity) => <tr key={activity.id}><td className="font-mono">{formatDate(activity.activityDate, { month: "short", day: "numeric" })}</td><td>{activity.fieldName}</td><td>{titleCase(activity.type)}</td><td className="numeric">{activity.costAmount === null ? "—" : formatCurrency(activity.costAmount)}</td><td className="numeric">{activity.revenueAmount === null ? "—" : formatCurrency(activity.revenueAmount)}</td></tr>)}</tbody></table></div></section>
    </article>
  </>;
}
