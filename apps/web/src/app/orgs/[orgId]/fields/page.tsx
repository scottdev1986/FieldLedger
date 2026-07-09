"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Archive, Edit3, Lock, Plus, Rows3 } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { ActivityBadge, StatusBadge } from "@/components/ui/Badge";
import { Button, buttonClasses } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { EmptyState, ErrorState, PageHeader } from "@/components/ui/Primitives";
import { AppSkeleton } from "@/components/ui/Skeleton";
import { useToast } from "@/components/ui/Toast";
import { FieldDrawer } from "@/features/fields/FieldDrawer";
import { useCurrentOrg } from "@/features/orgs/OrgContext";
import { apiFetch } from "@/lib/api-client";
import type { FieldListItem, FieldsResponse } from "@/lib/api-types";
import { cx, formatDate, formatNumber } from "@/lib/format";
import { queryKeys } from "@/lib/query-keys";

export default function FieldsPage() {
  const org = useCurrentOrg(); const canEdit = org.role !== "viewer"; const atLimit = org.plan === "free" && org.activeFieldCount >= 3;
  const client = useQueryClient(); const { toast } = useToast();
  const [drawer, setDrawer] = useState<"create" | FieldListItem | null>(null); const [archiveField, setArchiveField] = useState<FieldListItem | null>(null); const [limitHint, setLimitHint] = useState(false);
  const query = useQuery({ queryKey: queryKeys.fields(org.id), queryFn: () => apiFetch<FieldsResponse>(`/api/orgs/${org.id}/fields`) });
  const archive = useMutation({ mutationFn: (fieldId: string) => apiFetch<void>(`/api/orgs/${org.id}/fields/${fieldId}`, { method: "DELETE" }), onSuccess: async () => { await Promise.all([client.invalidateQueries({ queryKey: queryKeys.fields(org.id) }), client.invalidateQueries({ queryKey: queryKeys.dashboard(org.id) }), client.invalidateQueries({ queryKey: queryKeys.orgs })]); toast({ type: "success", title: "Field archived" }); setArchiveField(null); }, onError: (error) => toast({ type: "danger", title: "Could not archive field", description: error instanceof Error ? error.message : undefined }) });
  if (query.isLoading) return <AppSkeleton />;
  const action = canEdit ? atLimit ? <div className="relative"><Button aria-disabled="true" onClick={() => setLimitHint((value) => !value)} variant="secondary"><Lock className="h-4 w-4" />Add field</Button>{limitHint ? <div className="absolute right-0 top-12 z-20 w-72 rounded-lg border border-line bg-surface p-4 shadow-pop"><p className="overline text-brand-700">Pro</p><p className="mt-2 text-[13px] text-ink">Free organizations can have three active fields.</p>{org.role === "owner" ? <Link className={cx(buttonClasses("primary", "sm"), "mt-3")} href={`/orgs/${org.id}/billing`}>Upgrade to Pro</Link> : <p className="mt-2 text-xs text-ink-soft">Ask an owner to upgrade.</p>}</div> : null}</div> : <Button onClick={() => setDrawer("create")}><Plus className="h-4 w-4" />Add field</Button> : undefined;
  return <>
    <PageHeader action={action} description="Active and archived ground, current crops, and the latest work logged." overline={org.name} title="Fields" />
    {query.error ? <ErrorState error={query.error} onRetry={() => void query.refetch()} /> : query.data?.fields.length ? <div className="card overflow-hidden p-0"><div className="overflow-x-auto"><table className="ledger-table"><thead><tr><th>Name</th><th>Crop</th><th className="numeric">Acreage</th><th>Last activity</th><th>Status</th>{canEdit ? <th><span className="sr-only">Actions</span></th> : null}</tr></thead><tbody>{query.data.fields.map((field) => <tr key={field.id}><td><Link className="font-medium text-ink hover:text-brand-700" href={`/orgs/${org.id}/fields/${field.id}`}>{field.name}</Link></td><td>{field.currentCrop ?? field.defaultCrop}</td><td className="numeric">{formatNumber(field.acreage)} ac</td><td>{field.lastActivity ? <div className="flex items-center gap-2"><ActivityBadge type={field.lastActivity.type} /><time className="font-mono text-xs text-ink-faint">{formatDate(field.lastActivity.activityDate, { month: "short", day: "numeric" })}</time></div> : <span className="text-ink-faint">—</span>}</td><td><StatusBadge status={field.status} /></td>{canEdit ? <td><div className="flex justify-end gap-1"><Button aria-label={`Edit ${field.name}`} onClick={() => setDrawer(field)} size="sm" variant="ghost"><Edit3 className="h-3.5 w-3.5" /></Button>{field.status === "active" ? <Button aria-label={`Archive ${field.name}`} onClick={() => setArchiveField(field)} size="sm" variant="ghost"><Archive className="h-3.5 w-3.5" /></Button> : null}</div></td> : null}</tr>)}</tbody></table></div></div> : <div className="card p-0"><EmptyState action={canEdit ? <Button onClick={() => setDrawer("create")}><Plus className="h-4 w-4" />Add field</Button> : undefined} description="Add a field to start recording seasonal operations." icon={Rows3} title="No fields yet" /></div>}
    {drawer ? <FieldDrawer field={drawer === "create" ? undefined : drawer} onClose={() => setDrawer(null)} onLimitReached={() => { setDrawer(null); setLimitHint(true); }} orgId={org.id} /> : null}
    <Dialog description={archiveField ? `${archiveField.name} will remain in historical reports but no longer counts toward active-field usage.` : undefined} footer={<><Button onClick={() => setArchiveField(null)} variant="secondary">Cancel</Button><Button loading={archive.isPending} onClick={() => archiveField && archive.mutate(archiveField.id)} variant="destructive">Archive field</Button></>} onClose={() => setArchiveField(null)} open={Boolean(archiveField)} title="Archive this field?" variant="modal"><p className="text-sm text-ink-soft">You can still view the field and its past seasons after archiving.</p></Dialog>
  </>;
}
