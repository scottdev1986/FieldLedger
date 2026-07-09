"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2, Users } from "lucide-react";
import { useState } from "react";
import { RoleBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { EmptyState, ErrorState, PageHeader } from "@/components/ui/Primitives";
import { AppSkeleton } from "@/components/ui/Skeleton";
import { useToast } from "@/components/ui/Toast";
import { InviteMemberDrawer } from "@/features/members/InviteMemberDrawer";
import { useCurrentOrg } from "@/features/orgs/OrgContext";
import { apiFetch } from "@/lib/api-client";
import type { Member, MemberResponse, MembersResponse, Role } from "@/lib/api-types";
import { formatDate, initials } from "@/lib/format";
import { queryKeys } from "@/lib/query-keys";

export default function MembersPage() {
  const org = useCurrentOrg(); const owner = org.role === "owner"; const client = useQueryClient(); const { toast } = useToast();
  const [invite, setInvite] = useState(false); const [removeMember, setRemoveMember] = useState<Member | null>(null);
  const query = useQuery({ queryKey: queryKeys.members(org.id), queryFn: () => apiFetch<MembersResponse>(`/api/orgs/${org.id}/members`) });
  const update = useMutation({ mutationFn: ({ userId, role }: { userId: string; role: Role }) => apiFetch<MemberResponse>(`/api/orgs/${org.id}/members/${userId}`, { method: "PATCH", body: JSON.stringify({ role }) }), onSuccess: async () => { await client.invalidateQueries({ queryKey: queryKeys.members(org.id) }); toast({ type: "success", title: "Member role updated" }); }, onError: (error) => { void client.invalidateQueries({ queryKey: queryKeys.members(org.id) }); toast({ type: "danger", title: "Could not change role", description: error instanceof Error ? error.message : undefined }); } });
  const remove = useMutation({ mutationFn: (userId: string) => apiFetch<void>(`/api/orgs/${org.id}/members/${userId}`, { method: "DELETE" }), onSuccess: async () => { await client.invalidateQueries({ queryKey: queryKeys.members(org.id) }); toast({ type: "success", title: "Member removed" }); setRemoveMember(null); }, onError: (error) => toast({ type: "danger", title: "Could not remove member", description: error instanceof Error ? error.message : undefined }) });
  if (query.isLoading) return <AppSkeleton />;
  return <>
    <PageHeader action={owner ? <Button onClick={() => setInvite(true)}><Plus className="h-4 w-4" />Invite member</Button> : undefined} description="Organization access and operating roles." overline={org.name} title="Members" />
    {query.error ? <ErrorState error={query.error} onRetry={() => void query.refetch()} /> : query.data?.members.length ? <div className="card overflow-hidden p-0"><div className="overflow-x-auto"><table className="ledger-table"><thead><tr><th>Member</th><th>Email</th><th>Role</th><th>Joined</th>{owner ? <th><span className="sr-only">Member actions</span></th> : null}</tr></thead><tbody>{query.data.members.map((member) => <tr key={member.userId}><td><div className="flex items-center gap-3"><span className="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-brand-100 text-[11px] font-semibold text-brand-800">{initials(member.displayName)}</span><span className="font-medium">{member.displayName}</span></div></td><td className="font-mono text-[13px]">{member.email}</td><td>{owner ? <select aria-label={`Role for ${member.displayName}`} className="h-8 rounded-md border border-line-strong bg-surface px-2 text-[13px]" disabled={update.isPending} onChange={(event) => update.mutate({ userId: member.userId, role: event.target.value as Role })} value={member.role}><option value="owner">Owner</option><option value="agronomist">Agronomist</option><option value="viewer">Viewer</option></select> : <RoleBadge role={member.role} />}</td><td className="font-mono text-xs text-ink-faint">{formatDate(member.joinedAt)}</td>{owner ? <td><div className="flex justify-end"><Button aria-label={`Remove ${member.displayName}`} onClick={() => setRemoveMember(member)} size="sm" variant="ghost"><Trash2 className="h-3.5 w-3.5" /></Button></div></td> : null}</tr>)}</tbody></table></div></div> : <div className="card p-0"><EmptyState action={owner ? <Button onClick={() => setInvite(true)}><Plus className="h-4 w-4" />Invite member</Button> : undefined} description="Add registered users to collaborate on this farm ledger." icon={Users} title="No members found" /></div>}
    {!owner ? <p className="mt-4 text-[13px] text-ink-soft">Only owners can invite members or change roles.</p> : null}
    {invite ? <InviteMemberDrawer onClose={() => setInvite(false)} orgId={org.id} /> : null}
    <Dialog description={removeMember ? `${removeMember.displayName} will lose access to ${org.name}.` : undefined} footer={<><Button onClick={() => setRemoveMember(null)} variant="secondary">Cancel</Button><Button loading={remove.isPending} onClick={() => removeMember && remove.mutate(removeMember.userId)} variant="destructive">Remove member</Button></>} onClose={() => setRemoveMember(null)} open={Boolean(removeMember)} title="Remove this member?" variant="modal"><p className="text-sm text-ink-soft">The last owner cannot be removed or demoted.</p></Dialog>
  </>;
}
