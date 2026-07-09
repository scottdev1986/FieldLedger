"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { FieldError } from "@/components/ui/Primitives";
import { useToast } from "@/components/ui/Toast";
import { apiFetch, ApiError } from "@/lib/api-client";
import type { FieldInput, FieldListItem, FieldResponse } from "@/lib/api-types";
import { queryKeys } from "@/lib/query-keys";

const schema = z.object({ name: z.string().trim().min(2, "Enter a field name.").max(100), acreage: z.number({ invalid_type_error: "Enter acreage." }).positive("Acreage must be greater than zero."), defaultCrop: z.string().trim().min(2, "Enter the default crop.").max(60), soilType: z.string().trim().max(80).nullable() });

export function FieldDrawer({ orgId, field, onClose, onLimitReached }: { orgId: string; field?: FieldListItem; onClose: () => void; onLimitReached?: () => void }) {
  const client = useQueryClient();
  const { toast } = useToast();
  const { register, handleSubmit, formState: { errors } } = useForm<FieldInput>({ resolver: zodResolver(schema), defaultValues: { name: field?.name ?? "", acreage: field?.acreage ?? 0, defaultCrop: field?.defaultCrop ?? "", soilType: field?.soilType ?? null } });
  const mutation = useMutation({
    mutationFn: (values: FieldInput) => apiFetch<FieldResponse>(field ? `/api/orgs/${orgId}/fields/${field.id}` : `/api/orgs/${orgId}/fields`, { method: field ? "PATCH" : "POST", body: JSON.stringify(values) }),
    onSuccess: async () => { await Promise.all([client.invalidateQueries({ queryKey: queryKeys.fields(orgId) }), client.invalidateQueries({ queryKey: queryKeys.dashboard(orgId) }), client.invalidateQueries({ queryKey: queryKeys.orgs }), field ? client.invalidateQueries({ queryKey: queryKeys.field(orgId, field.id) }) : Promise.resolve()]); toast({ type: "success", title: field ? "Field updated" : "Field added" }); onClose(); },
    onError: (error) => { if (error instanceof ApiError && error.code === "field_limit_reached") onLimitReached?.(); toast({ type: "danger", title: "Could not save field", description: error instanceof Error ? error.message : undefined }); },
  });
  return (
    <Dialog footer={<><Button onClick={onClose} variant="secondary">Cancel</Button><Button loading={mutation.isPending} onClick={handleSubmit((values) => mutation.mutate(values))}>{field ? "Save changes" : "Add field"}</Button></>} onClose={onClose} open title={field ? "Edit field" : "Add field"}>
      <form className="space-y-4" onSubmit={handleSubmit((values) => mutation.mutate(values))}>
        <div><label className="form-label" htmlFor="field-name">Field name</label><input className="form-control" id="field-name" aria-invalid={Boolean(errors.name)} aria-describedby={errors.name ? "field-name-error" : undefined} {...register("name")} /><FieldError id="field-name-error" message={errors.name?.message} /></div>
        <div><label className="form-label" htmlFor="field-acreage">Acreage</label><input className="form-control font-mono" id="field-acreage" min="0.1" step="0.1" type="number" aria-invalid={Boolean(errors.acreage)} aria-describedby={errors.acreage ? "field-acreage-error" : undefined} {...register("acreage", { valueAsNumber: true })} /><FieldError id="field-acreage-error" message={errors.acreage?.message} /></div>
        <div><label className="form-label" htmlFor="field-crop">Default crop</label><input className="form-control" id="field-crop" placeholder="Corn" aria-invalid={Boolean(errors.defaultCrop)} aria-describedby={errors.defaultCrop ? "field-crop-error" : undefined} {...register("defaultCrop")} /><FieldError id="field-crop-error" message={errors.defaultCrop?.message} /></div>
        <div><label className="form-label" htmlFor="field-soil">Soil type <span className="font-normal text-ink-faint">(optional)</span></label><input className="form-control" id="field-soil" placeholder="Silty clay loam" aria-invalid={Boolean(errors.soilType)} aria-describedby={errors.soilType ? "field-soil-error" : undefined} {...register("soilType", { setValueAs: (value) => value === "" ? null : value })} /><FieldError id="field-soil-error" message={errors.soilType?.message} /></div>
      </form>
    </Dialog>
  );
}
