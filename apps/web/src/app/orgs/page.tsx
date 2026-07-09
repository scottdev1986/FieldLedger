"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Building2, ChevronRight, LogOut, Plus } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { useState } from "react";
import { z } from "zod";
import { Logo } from "@/components/Logo";
import { PlanBadge, RoleBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { EmptyState, ErrorState, FieldError, PageHeader } from "@/components/ui/Primitives";
import { AppSkeleton } from "@/components/ui/Skeleton";
import { useToast } from "@/components/ui/Toast";
import { useAuth } from "@/features/auth/AuthProvider";
import { RequireAuth } from "@/features/auth/RequireAuth";
import { apiFetch } from "@/lib/api-client";
import type { OrganizationResponse, OrganizationsResponse } from "@/lib/api-types";
import { formatNumber, initials } from "@/lib/format";
import { queryKeys } from "@/lib/query-keys";

const schema = z.object({ name: z.string().trim().min(2, "Enter an organization name.").max(100), slug: z.string().trim().regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, "Use lowercase letters, numbers, and hyphens.").max(80) });
type OrgValues = z.infer<typeof schema>;

function OrganizationsContent() {
  const { user, signOut, refreshMe } = useAuth();
  const { toast } = useToast();
  const router = useRouter();
  const client = useQueryClient();
  const [open, setOpen] = useState(false);
  const query = useQuery({ queryKey: queryKeys.orgs, queryFn: () => apiFetch<OrganizationsResponse>("/api/orgs") });
  const { register, handleSubmit, reset, formState: { errors } } = useForm<OrgValues>({ resolver: zodResolver(schema), defaultValues: { name: "", slug: "" } });
  const mutation = useMutation({
    mutationFn: (values: OrgValues) => apiFetch<OrganizationResponse>("/api/orgs", { method: "POST", body: JSON.stringify(values) }),
    onSuccess: async (result) => { await Promise.all([client.invalidateQueries({ queryKey: queryKeys.orgs }), refreshMe()]); toast({ type: "success", title: "Organization created" }); setOpen(false); reset(); router.push(`/orgs/${result.organization.id}`); },
    onError: (error) => toast({ type: "danger", title: "Could not create organization", description: error instanceof Error ? error.message : undefined }),
  });
  if (query.isLoading) return <AppSkeleton />;
  return (
    <div className="min-h-screen bg-paper">
      <header className="h-14 border-b border-line bg-surface"><div className="mx-auto flex h-full max-w-[1200px] items-center justify-between px-4 sm:px-6"><Logo /><div className="flex items-center gap-3"><span className="grid h-7 w-7 place-items-center rounded-full bg-brand-100 text-xs font-semibold text-brand-800">{initials(user?.displayName ?? "User")}</span><span className="hidden text-[13px] font-medium sm:block">{user?.displayName}</span><Button aria-label="Sign out" onClick={signOut} size="sm" variant="ghost"><LogOut className="h-4 w-4" /></Button></div></div></header>
      <main className="mx-auto max-w-[1200px] px-4 py-8 sm:px-6">
        <PageHeader action={<Button onClick={() => setOpen(true)}><Plus className="h-4 w-4" />Create organization</Button>} description="Choose a farm ledger or begin a new one." overline="FieldLedger" title="Your organizations" />
        {query.error ? <ErrorState error={query.error} onRetry={() => void query.refetch()} /> : query.data?.organizations.length ? (
          <div className="grid gap-4 md:grid-cols-2">
            {query.data.organizations.map((org) => (
              <Link className="group card block p-5 transition-colors hover:border-brand-300" href={`/orgs/${org.id}`} key={org.id}>
                <div className="flex items-start justify-between gap-4"><div><h2 className="font-display text-xl font-medium text-ink">{org.name}</h2><p className="mt-2 font-mono text-xs text-ink-faint">{formatNumber(org.activeFieldCount, 0)} fields · {formatNumber(org.seasonCount, 0)} seasons</p></div><ChevronRight className="h-5 w-5 text-ink-faint transition-transform group-hover:translate-x-0.5 group-hover:text-brand-600" /></div>
                <div className="mt-5 flex gap-2"><RoleBadge role={org.role} /><PlanBadge plan={org.plan} /></div>
              </Link>
            ))}
          </div>
        ) : <div className="card p-0"><EmptyState action={<Button onClick={() => setOpen(true)}><Plus className="h-4 w-4" />Create organization</Button>} description="Create your first farm organization to begin tracking fields and seasons." icon={Building2} title="No organizations yet" /></div>}
      </main>
      <Dialog footer={<><Button onClick={() => setOpen(false)} variant="secondary">Cancel</Button><Button loading={mutation.isPending} onClick={handleSubmit((values) => mutation.mutate(values))}>Create organization</Button></>} onClose={() => setOpen(false)} open={open} title="Create organization">
        <form className="space-y-4" onSubmit={handleSubmit((values) => mutation.mutate(values))}>
          <div><label className="form-label" htmlFor="org-name">Organization name</label><input className="form-control" id="org-name" aria-invalid={Boolean(errors.name)} aria-describedby={errors.name ? "org-name-error" : undefined} {...register("name")} /><FieldError id="org-name-error" message={errors.name?.message} /></div>
          <div><label className="form-label" htmlFor="org-slug">URL slug</label><input className="form-control font-mono" id="org-slug" placeholder="cedar-lane-farms" aria-invalid={Boolean(errors.slug)} aria-describedby={errors.slug ? "org-slug-error" : "org-slug-help"} {...register("slug")} /><p className="mt-1.5 text-xs text-ink-faint" id="org-slug-help">Lowercase letters, numbers, and hyphens.</p><FieldError id="org-slug-error" message={errors.slug?.message} /></div>
        </form>
      </Dialog>
    </div>
  );
}

export default function OrganizationsPage() { return <RequireAuth><OrganizationsContent /></RequireAuth>; }
