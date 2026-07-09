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
import type { SeasonInput, SeasonResponse } from "@/lib/api-types";
import { queryKeys } from "@/lib/query-keys";

const schema = z.object({ year: z.number().int().min(2000).max(2100), name: z.string().trim().min(2, "Enter a season name.").max(80), startsOn: z.string().min(1, "Choose a start date."), endsOn: z.string().min(1, "Choose an end date.") }).refine((value) => value.endsOn >= value.startsOn, { path: ["endsOn"], message: "End date must be after the start date." });

export function SeasonDrawer({ orgId, onClose }: { orgId: string; onClose: () => void }) {
  const client = useQueryClient(); const { toast } = useToast();
  const year = new Date().getFullYear();
  const { register, handleSubmit, formState: { errors } } = useForm<SeasonInput>({ resolver: zodResolver(schema), defaultValues: { year, name: `${year} Growing Season`, startsOn: `${year}-03-01`, endsOn: `${year}-11-30` } });
  const mutation = useMutation({ mutationFn: (values: SeasonInput) => apiFetch<SeasonResponse>(`/api/orgs/${orgId}/seasons`, { method: "POST", body: JSON.stringify(values) }), onSuccess: async () => { await Promise.all([client.invalidateQueries({ queryKey: queryKeys.seasons(orgId) }), client.invalidateQueries({ queryKey: queryKeys.dashboard(orgId) }), client.invalidateQueries({ queryKey: queryKeys.orgs })]); toast({ type: "success", title: "Season created" }); onClose(); }, onError: (error) => toast({ type: "danger", title: "Could not create season", description: error instanceof Error ? error.message : undefined }) });
  return <Dialog footer={<><Button onClick={onClose} variant="secondary">Cancel</Button><Button loading={mutation.isPending} onClick={handleSubmit((values) => mutation.mutate(values))}>Create season</Button></>} onClose={onClose} open title="Create season"><form className="space-y-4" onSubmit={handleSubmit((values) => mutation.mutate(values))}>
    <div><label className="form-label" htmlFor="season-year">Year</label><input className="form-control font-mono" id="season-year" type="number" aria-invalid={Boolean(errors.year)} aria-describedby={errors.year ? "season-year-error" : undefined} {...register("year", { valueAsNumber: true })} /><FieldError id="season-year-error" message={errors.year?.message} /></div>
    <div><label className="form-label" htmlFor="season-name">Name</label><input className="form-control" id="season-name" aria-invalid={Boolean(errors.name)} aria-describedby={errors.name ? "season-name-error" : undefined} {...register("name")} /><FieldError id="season-name-error" message={errors.name?.message} /></div>
    <div className="grid gap-4 sm:grid-cols-2"><div><label className="form-label" htmlFor="season-start">Starts</label><input className="form-control font-mono" id="season-start" type="date" aria-invalid={Boolean(errors.startsOn)} {...register("startsOn")} /><FieldError id="season-start-error" message={errors.startsOn?.message} /></div><div><label className="form-label" htmlFor="season-end">Ends</label><input className="form-control font-mono" id="season-end" type="date" aria-invalid={Boolean(errors.endsOn)} {...register("endsOn")} /><FieldError id="season-end-error" message={errors.endsOn?.message} /></div></div>
  </form></Dialog>;
}
