"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, Lock, Sprout } from "lucide-react";
import { useState } from "react";
import { PlanBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Card, ErrorState, Overline, PageHeader } from "@/components/ui/Primitives";
import { AppSkeleton } from "@/components/ui/Skeleton";
import { useToast } from "@/components/ui/Toast";
import { useCurrentOrg } from "@/features/orgs/OrgContext";
import { apiFetch, ApiError } from "@/lib/api-client";
import type { BillingResponse, Plan, PlanMutationResponse } from "@/lib/api-types";
import { cx, formatDate, formatNumber, titleCase } from "@/lib/format";
import { queryKeys } from "@/lib/query-keys";

export default function BillingPage() {
  const org = useCurrentOrg(); const owner = org.role === "owner"; const client = useQueryClient(); const { toast } = useToast();
  const [confirm, setConfirm] = useState<Plan | null>(null); const [downgradeError, setDowngradeError] = useState<string>();
  const query = useQuery({ queryKey: queryKeys.billing(org.id), queryFn: () => apiFetch<BillingResponse>(`/api/orgs/${org.id}/billing`) });
  const changePlan = useMutation({ mutationFn: (plan: Plan) => apiFetch<PlanMutationResponse>(`/api/orgs/${org.id}/billing/${plan === "pro" ? "upgrade" : "downgrade"}`, { method: "POST" }), onSuccess: async (_, plan) => { setConfirm(null); setDowngradeError(undefined); await Promise.all([client.invalidateQueries({ queryKey: queryKeys.billing(org.id) }), client.invalidateQueries({ queryKey: queryKeys.orgs }), client.invalidateQueries({ queryKey: queryKeys.dashboard(org.id) })]); toast({ type: "success", title: `Plan changed to ${titleCase(plan)}`, description: "The entitlement change is active immediately." }); }, onError: (error) => { setConfirm(null); if (error instanceof ApiError && error.code === "too_many_active_fields_for_free") { setDowngradeError(error.message); return; } toast({ type: "danger", title: "Could not change plan", description: error instanceof Error ? error.message : undefined }); } });
  if (query.isLoading) return <AppSkeleton />;
  if (query.error || !query.data) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  const billing = query.data; const max = billing.limits.maxFields; const usagePercent = max ? Math.min(100, (billing.usage.activeFieldCount / max) * 100) : 100;
  const entitlements = [{ label: "Active fields", enabled: true, detail: max ? `Up to ${max}` : "Unlimited" }, { label: "CSV activity exports", enabled: billing.limits.csvExportEnabled, detail: billing.limits.csvExportEnabled ? "Included" : "Pro only" }, { label: "Printable season reports", enabled: billing.limits.seasonReportEnabled, detail: billing.limits.seasonReportEnabled ? "Included" : "Pro only" }];
  return <>
    <PageHeader description="Plan entitlements, field usage, and the organization’s change history." overline={org.name} title="Plan & billing" />
    {downgradeError ? <div className="mb-4 rounded-lg border border-warn/25 bg-warn-bg p-4" role="alert"><p className="font-medium text-warn">Archive fields before downgrading</p><p className="mt-1 text-[13px] text-ink-soft">{downgradeError}</p></div> : null}
    <div className="grid gap-4 lg:grid-cols-[1.15fr_0.85fr]">
      <Card>
        <div className="flex items-center justify-between"><div><Overline>Current plan</Overline><h2 className="mt-3 text-2xl font-semibold tracking-tight">{titleCase(billing.plan)}</h2></div><PlanBadge plan={billing.plan} /></div>
        <div className="mt-6 divide-y divide-line border-y border-line">{entitlements.map((item) => <div className="flex items-center gap-3 py-3" key={item.label}>{item.enabled ? <Check className="h-4 w-4 text-brand-600" /> : <Lock className="h-4 w-4 text-ink-faint" />}<span className={cx("text-[13px]", !item.enabled && "text-ink-soft")}>{item.label}</span><span className="ml-auto font-mono text-xs text-ink-faint">{item.detail}</span></div>)}</div>
        <div className="mt-6"><div className="flex items-center justify-between gap-3"><p className="font-mono text-[13px] text-ink">Active fields — {formatNumber(billing.usage.activeFieldCount, 0)}{max ? ` of ${max}` : " · unlimited"}</p><Sprout className="h-4 w-4 text-ink-faint" /></div><div className="mt-3 h-2 overflow-hidden rounded-full bg-sunken"><div className={cx("h-full rounded-full", max && billing.usage.activeFieldCount >= max ? "bg-warn" : "bg-brand-600")} style={{ width: `${usagePercent}%` }} /></div></div>
        <div className="mt-7">{owner ? billing.plan === "free" ? <Button onClick={() => setConfirm("pro")}>Upgrade to Pro</Button> : <Button onClick={() => setConfirm("free")} variant="secondary">Downgrade to Free</Button> : <p className="text-[13px] text-ink-soft">Only owners can change the plan.</p>}<p className="mt-3 text-xs text-ink-faint">Plan changes update demo entitlements only. No payment is processed.</p></div>
      </Card>
      <Card><Overline>Plan-change history</Overline>{billing.history.length ? <div className="mt-4 divide-y divide-line">{billing.history.map((entry, index) => <div className="py-4 first:pt-0" key={`${entry.changedAt}-${index}`}><div className="flex items-center justify-between gap-4"><p className="font-mono text-[13px] text-ink">{titleCase(entry.fromPlan)} → {titleCase(entry.toPlan)}</p><time className="font-mono text-xs text-ink-faint">{formatDate(entry.changedAt)}</time></div><p className="mt-1 text-xs text-ink-soft">Changed by {entry.changedBy}</p></div>)}</div> : <div className="py-12 text-center"><p className="text-[15px] font-semibold">No plan changes yet</p><p className="mt-1 text-[13px] text-ink-soft">Updates will be recorded here.</p></div>}</Card>
    </div>
    <Dialog description={confirm === "pro" ? "This enables unlimited active fields, CSV exports, and season reports immediately." : "Free allows three active fields and disables exports and season reports."} footer={<><Button onClick={() => setConfirm(null)} variant="secondary">Cancel</Button><Button loading={changePlan.isPending} onClick={() => confirm && changePlan.mutate(confirm)}>{confirm === "pro" ? "Upgrade to Pro" : "Downgrade to Free"}</Button></>} onClose={() => setConfirm(null)} open={Boolean(confirm)} title={confirm === "pro" ? "Upgrade this organization?" : "Downgrade this organization?"} variant="modal"><p className="text-sm text-ink-soft">This is an in-app entitlement change for the demo. No payment or refund occurs.</p></Dialog>
  </>;
}
