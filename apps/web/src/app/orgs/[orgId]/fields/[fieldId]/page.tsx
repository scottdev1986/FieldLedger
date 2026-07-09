"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClipboardList, Edit3, Plus } from "lucide-react";
import { useParams } from "next/navigation";
import { useState, type KeyboardEvent } from "react";
import { StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Card, EmptyState, ErrorState, Overline, PageHeader } from "@/components/ui/Primitives";
import { AppSkeleton } from "@/components/ui/Skeleton";
import { useToast } from "@/components/ui/Toast";
import { ActivityDrawer } from "@/features/activities/ActivityDrawer";
import { ActivityTimeline } from "@/features/activities/ActivityTimeline";
import { FieldDrawer } from "@/features/fields/FieldDrawer";
import { useCurrentOrg } from "@/features/orgs/OrgContext";
import { StatCard } from "@/features/orgs/StatCard";
import { apiFetch } from "@/lib/api-client";
import type { ActivitiesResponse, Activity, FieldResponse, SeasonsResponse } from "@/lib/api-types";
import { cx, formatCurrency, formatDate, formatNumber } from "@/lib/format";
import { queryKeys } from "@/lib/query-keys";

type Tab = "activity" | "seasons";

export default function FieldDetailPage() {
  const org = useCurrentOrg(); const { fieldId } = useParams<{ fieldId: string }>(); const canEdit = org.role !== "viewer";
  const client = useQueryClient(); const { toast } = useToast();
  const [tab, setTab] = useState<Tab>("activity"); const [selectedSeason, setSelectedSeason] = useState(""); const [activityDrawer, setActivityDrawer] = useState<"create" | Activity | null>(null); const [deleteActivity, setDeleteActivity] = useState<Activity | null>(null); const [fieldEdit, setFieldEdit] = useState(false);
  const fieldQuery = useQuery({ queryKey: queryKeys.field(org.id, fieldId), queryFn: () => apiFetch<FieldResponse>(`/api/orgs/${org.id}/fields/${fieldId}`) });
  const seasonsQuery = useQuery({ queryKey: queryKeys.seasons(org.id), queryFn: () => apiFetch<SeasonsResponse>(`/api/orgs/${org.id}/seasons`) });
  const effectiveSeason = selectedSeason || seasonsQuery.data?.seasons[0]?.id || "";
  const activitiesQuery = useQuery({ queryKey: queryKeys.activities(org.id, fieldId, effectiveSeason), queryFn: () => apiFetch<ActivitiesResponse>(`/api/orgs/${org.id}/fields/${fieldId}/activities?seasonId=${encodeURIComponent(effectiveSeason)}`), enabled: Boolean(effectiveSeason) });
  const remove = useMutation({ mutationFn: (activityId: string) => apiFetch<void>(`/api/orgs/${org.id}/activities/${activityId}`, { method: "DELETE" }), onSuccess: async () => { await Promise.all([client.invalidateQueries({ queryKey: ["orgs", org.id, "fields", fieldId, "activities"] }), client.invalidateQueries({ queryKey: queryKeys.field(org.id, fieldId) }), client.invalidateQueries({ queryKey: queryKeys.dashboard(org.id) })]); toast({ type: "success", title: "Activity deleted" }); setDeleteActivity(null); }, onError: (error) => toast({ type: "danger", title: "Could not delete activity", description: error instanceof Error ? error.message : undefined }) });
  if (fieldQuery.isLoading || seasonsQuery.isLoading) return <AppSkeleton />;
  if (fieldQuery.error || !fieldQuery.data) return <ErrorState error={fieldQuery.error} onRetry={() => void fieldQuery.refetch()} />;
  const { field, seasonRollups } = fieldQuery.data;
  const selectedRollup = seasonRollups.find((rollup) => rollup.seasonId === effectiveSeason) ?? seasonRollups[0];
  const switchTab = (event: KeyboardEvent<HTMLDivElement>) => { if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return; event.preventDefault(); setTab((value) => value === "activity" ? "seasons" : "activity"); };
  return <>
    <PageHeader action={canEdit ? <><Button onClick={() => setFieldEdit(true)} variant="secondary"><Edit3 className="h-4 w-4" />Edit field</Button>{seasonsQuery.data?.seasons.length ? <Button onClick={() => setActivityDrawer("create")}><Plus className="h-4 w-4" />Log activity</Button> : null}</> : undefined} description={`${formatNumber(field.acreage)} ac · ${field.currentCrop ?? field.defaultCrop}`} overline={org.name} title={field.name} />
    <div className="card mb-4 flex flex-wrap items-center gap-x-8 gap-y-3 py-4"><div><Overline>Status</Overline><div className="mt-1.5"><StatusBadge status={field.status} /></div></div><div><Overline>Soil</Overline><p className="mt-1.5 text-[13px] text-ink">{field.soilType ?? "Not recorded"}</p></div><div><Overline>Planted</Overline><p className="mt-1.5 font-mono text-[13px] text-ink">{selectedRollup?.plantedOn ? formatDate(selectedRollup.plantedOn) : "Not recorded"}</p></div><div className="ml-auto min-w-52"><label className="form-label" htmlFor="season-selector">Season</label><select className="form-control" id="season-selector" value={effectiveSeason} onChange={(event) => setSelectedSeason(event.target.value)}>{seasonsQuery.data?.seasons.map((season) => <option key={season.id} value={season.id}>{season.name}</option>)}</select></div></div>
    {selectedRollup ? <div className="mb-4 grid gap-4 md:grid-cols-3"><StatCard context={selectedRollup.crop} label="Yield per acre" unit={selectedRollup.yieldPerAcre === null ? undefined : "bu/ac"} value={selectedRollup.yieldPerAcre === null ? "—" : formatNumber(selectedRollup.yieldPerAcre)} /><StatCard context={`${formatCurrency(selectedRollup.inputCost)} in inputs`} label="Season cost" value={formatCurrency(selectedRollup.inputCost, true)} /><StatCard context={`${formatCurrency(selectedRollup.harvestValue)} harvest value`} label="Net value" value={formatCurrency(selectedRollup.netValue, true)} /></div> : null}
    <div className="mb-4 flex border-b border-line" onKeyDown={switchTab} role="tablist" aria-label="Field sections">{(["activity", "seasons"] as Tab[]).map((item) => <button aria-selected={tab === item} className={cx("-mb-px border-b-2 px-4 py-3 text-[13px] font-medium capitalize", tab === item ? "border-brand-600 text-brand-700" : "border-transparent text-ink-soft hover:text-ink")} id={`${item}-tab`} key={item} onClick={() => setTab(item)} role="tab" tabIndex={tab === item ? 0 : -1}>{item}</button>)}</div>
    {tab === "activity" ? <Card role="tabpanel" aria-labelledby="activity-tab"><div className="mb-5 flex items-center justify-between"><div><Overline>Activity timeline</Overline><p className="mt-2 text-xs text-ink-soft">Operations recorded for the selected season.</p></div>{canEdit && seasonsQuery.data?.seasons.length ? <Button onClick={() => setActivityDrawer("create")} size="sm"><Plus className="h-3.5 w-3.5" />Log activity</Button> : null}</div>{activitiesQuery.isLoading ? <div className="space-y-4">{[1,2,3].map((i) => <div className="h-16 animate-pulse rounded-sm bg-sunken" key={i} />)}</div> : activitiesQuery.error ? <ErrorState error={activitiesQuery.error} onRetry={() => void activitiesQuery.refetch()} /> : activitiesQuery.data?.activities.length ? <ActivityTimeline activities={activitiesQuery.data.activities} editable={canEdit} onDelete={setDeleteActivity} onEdit={setActivityDrawer} /> : <EmptyState action={canEdit && seasonsQuery.data?.seasons.length ? <Button onClick={() => setActivityDrawer("create")}><Plus className="h-4 w-4" />Log activity</Button> : undefined} description="Log planting, input, irrigation, harvest, or notes for this season." icon={ClipboardList} title="No activity this season" />}</Card> : <div className="card overflow-hidden p-0" role="tabpanel" aria-labelledby="seasons-tab"><div className="overflow-x-auto"><table className="ledger-table"><thead><tr><th>Season</th><th>Crop</th><th className="numeric">Yield/acre</th><th className="numeric">Harvest value</th><th className="numeric">Vs. prior</th></tr></thead><tbody>{seasonRollups.map((rollup) => <tr key={rollup.seasonId}><td className="font-medium">{rollup.seasonName}</td><td>{rollup.crop}</td><td className="numeric">{rollup.yieldPerAcre === null ? "—" : `${formatNumber(rollup.yieldPerAcre)} bu`}</td><td className="numeric">{formatCurrency(rollup.harvestValue)}</td><td className={cx("numeric", rollup.priorYieldDeltaPercent !== null && (rollup.priorYieldDeltaPercent >= 0 ? "text-success" : "text-danger"))}>{rollup.priorYieldDeltaPercent === null ? "—" : `${rollup.priorYieldDeltaPercent >= 0 ? "▲ +" : "▼ "}${formatNumber(rollup.priorYieldDeltaPercent)}%`}</td></tr>)}</tbody></table></div></div>}
    {fieldEdit ? <FieldDrawer field={field} onClose={() => setFieldEdit(false)} orgId={org.id} /> : null}
    {activityDrawer ? <ActivityDrawer activity={activityDrawer === "create" ? undefined : activityDrawer} fieldId={fieldId} onClose={() => setActivityDrawer(null)} orgId={org.id} seasons={seasonsQuery.data?.seasons ?? []} selectedSeasonId={effectiveSeason} /> : null}
    <Dialog description="This removes the activity from season totals and reports." footer={<><Button onClick={() => setDeleteActivity(null)} variant="secondary">Cancel</Button><Button loading={remove.isPending} onClick={() => deleteActivity && remove.mutate(deleteActivity.id)} variant="destructive">Delete activity</Button></>} onClose={() => setDeleteActivity(null)} open={Boolean(deleteActivity)} title="Delete this activity?" variant="modal"><p className="text-sm text-ink-soft">This action cannot be undone.</p></Dialog>
  </>;
}
