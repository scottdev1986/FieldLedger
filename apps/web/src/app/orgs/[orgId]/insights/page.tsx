"use client";

import { useQuery } from "@tanstack/react-query";
import { Download, Lock } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { Button, buttonClasses } from "@/components/ui/Button";
import { ErrorState, PageHeader } from "@/components/ui/Primitives";
import { AppSkeleton } from "@/components/ui/Skeleton";
import { useToast } from "@/components/ui/Toast";
import { InsightsCharts } from "@/features/insights/Charts";
import { useCurrentOrg } from "@/features/orgs/OrgContext";
import { apiFetch, apiFetchBlob } from "@/lib/api-client";
import type { InsightsResponse } from "@/lib/api-types";
import { cx } from "@/lib/format";
import { queryKeys } from "@/lib/query-keys";

export default function InsightsPage() {
  const org = useCurrentOrg(); const { toast } = useToast(); const [exporting, setExporting] = useState(false); const [lockOpen, setLockOpen] = useState(false);
  const query = useQuery({ queryKey: queryKeys.insights(org.id), queryFn: () => apiFetch<InsightsResponse>(`/api/orgs/${org.id}/insights`) });
  const download = async () => { if (!query.data) return; setExporting(true); try { const blob = await apiFetchBlob(`/api/orgs/${org.id}/exports/activities.csv?seasonId=${encodeURIComponent(query.data.selectedSeasonId)}`); const url = URL.createObjectURL(blob); const anchor = document.createElement("a"); anchor.href = url; anchor.download = `fieldledger-activities-${query.data.selectedSeasonId}.csv`; anchor.click(); URL.revokeObjectURL(url); toast({ type: "success", title: "CSV export downloaded" }); } catch (error) { toast({ type: "danger", title: "Export failed", description: error instanceof Error ? error.message : undefined }); } finally { setExporting(false); } };
  if (query.isLoading) return <AppSkeleton />;
  const action = org.role !== "viewer" ? org.plan === "pro" ? <Button loading={exporting} onClick={() => void download()} variant="secondary"><Download className="h-4 w-4" />Export CSV</Button> : <div className="relative"><Button aria-disabled="true" onClick={() => setLockOpen((value) => !value)} variant="secondary"><Lock className="h-4 w-4" />Export CSV</Button>{lockOpen ? <div className="absolute right-0 top-12 z-20 w-72 rounded-lg border border-line bg-surface p-4 shadow-pop"><p className="overline text-brand-700">Pro</p><p className="mt-2 text-[13px]">CSV exports are included in Pro.</p>{org.role === "owner" ? <Link className={cx(buttonClasses("primary", "sm"), "mt-3")} href={`/orgs/${org.id}/billing`}>Upgrade to Pro</Link> : <p className="mt-2 text-xs text-ink-soft">Ask an owner to upgrade.</p>}</div> : null}</div> : undefined;
  return <><PageHeader action={action} description="Cross-season yield, costs, crop allocation, and field profitability." overline={org.name} title="Insights" />{query.error || !query.data ? <ErrorState error={query.error} onRetry={() => void query.refetch()} /> : <InsightsCharts data={query.data} />}</>;
}
