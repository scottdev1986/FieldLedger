"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { FieldError } from "@/components/ui/Primitives";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-client";
import type { InviteMemberInput, MemberResponse } from "@/lib/api-types";
import { queryKeys } from "@/lib/query-keys";

const schema = z.object({ email: z.string().email("Enter a valid email address."), role: z.enum(["owner", "agronomist", "viewer"]) });
export function InviteMemberDrawer({ orgId, onClose }: { orgId: string; onClose: () => void }) {
  const client = useQueryClient(); const { toast } = useToast();
  const { register, handleSubmit, formState: { errors } } = useForm<InviteMemberInput>({ resolver: zodResolver(schema), defaultValues: { email: "", role: "viewer" } });
  const mutation = useMutation({ mutationFn: (values: InviteMemberInput) => apiFetch<MemberResponse>(`/api/orgs/${orgId}/members`, { method: "POST", body: JSON.stringify(values) }), onSuccess: async () => { await client.invalidateQueries({ queryKey: queryKeys.members(orgId) }); toast({ type: "success", title: "Member added" }); onClose(); }, onError: (error) => toast({ type: "danger", title: "Could not add member", description: error instanceof Error ? error.message : undefined }) });
  return <Dialog description="The person must already have a FieldLedger account." footer={<><Button onClick={onClose} variant="secondary">Cancel</Button><Button loading={mutation.isPending} onClick={handleSubmit((values) => mutation.mutate(values))}>Add member</Button></>} onClose={onClose} open title="Invite member"><form className="space-y-4" onSubmit={handleSubmit((values) => mutation.mutate(values))}><div><label className="form-label" htmlFor="invite-email">Email</label><input className="form-control font-mono" id="invite-email" type="email" aria-invalid={Boolean(errors.email)} {...register("email")} /><FieldError id="invite-email-error" message={errors.email?.message} /></div><div><label className="form-label" htmlFor="invite-role">Role</label><select className="form-control" id="invite-role" {...register("role")}><option value="viewer">Viewer</option><option value="agronomist">Agronomist</option><option value="owner">Owner</option></select><p className="mt-1.5 text-xs text-ink-faint">Owners manage members and plan. Agronomists edit operations. Viewers are read-only.</p></div></form></Dialog>;
}
