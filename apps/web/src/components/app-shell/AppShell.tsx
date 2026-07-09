"use client";

import { useQuery } from "@tanstack/react-query";
import {
  BarChart3,
  BookOpenText,
  ChevronDown,
  CreditCard,
  LayoutDashboard,
  Lock,
  LogOut,
  Menu,
  PanelsTopLeft,
  Users,
  X,
} from "lucide-react";
import Link from "next/link";
import { useParams, usePathname } from "next/navigation";
import { useEffect, useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { Logo } from "@/components/Logo";
import { PlanBadge, RoleBadge } from "@/components/ui/Badge";
import { AppSkeleton } from "@/components/ui/Skeleton";
import { useAuth } from "@/features/auth/AuthProvider";
import { RequireAuth } from "@/features/auth/RequireAuth";
import { OrgProvider } from "@/features/orgs/OrgContext";
import { apiFetch } from "@/lib/api-client";
import type { OrganizationsResponse, OrganizationSummary } from "@/lib/api-types";
import { cx, initials } from "@/lib/format";
import { queryKeys } from "@/lib/query-keys";

const navigation = [
  { label: "Dashboard", segment: "", icon: LayoutDashboard },
  { label: "Fields", segment: "fields", icon: PanelsTopLeft },
  { label: "Insights", segment: "insights", icon: BarChart3 },
  { label: "Season report", segment: "report", icon: BookOpenText },
  { label: "Members", segment: "members", icon: Users },
  { label: "Plan & billing", segment: "billing", icon: CreditCard },
] as const;

function menuKeyDown(event: KeyboardEvent<HTMLElement>) {
  if (!["ArrowDown", "ArrowUp"].includes(event.key)) return;
  event.preventDefault();
  const items = [...event.currentTarget.querySelectorAll<HTMLElement>("[role='menuitem']")];
  const current = items.indexOf(document.activeElement as HTMLElement);
  const next = event.key === "ArrowDown" ? (current + 1) % items.length : (current - 1 + items.length) % items.length;
  items[next]?.focus();
}

function OrgSwitcher({ organization, orgs }: { organization: OrganizationSummary; orgs: OrganizationSummary[] }) {
  const [open, setOpen] = useState(false);
  const wrapper = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const close = (event: MouseEvent) => !wrapper.current?.contains(event.target as Node) && setOpen(false);
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, []);
  return (
    <div className="relative mt-5" ref={wrapper}>
      <button
        aria-expanded={open}
        aria-haspopup="menu"
        className="flex min-h-10 w-full items-center justify-between gap-3 rounded-md border border-line-strong bg-surface px-3 py-2 text-left hover:bg-sunken"
        onClick={() => setOpen((value) => !value)}
      >
        <span className="min-w-0 truncate text-[13px] font-medium text-ink">{organization.name}</span>
        <ChevronDown aria-hidden="true" className="h-4 w-4 shrink-0 text-ink-faint" />
      </button>
      {open ? (
        <div className="absolute left-0 top-[46px] z-30 w-full min-w-[260px] rounded-lg border border-line bg-surface p-1.5 shadow-pop" onKeyDown={menuKeyDown} role="menu">
          {orgs.map((org) => (
            <Link className="flex items-center justify-between gap-3 rounded-md px-2.5 py-2 text-[13px] hover:bg-sunken" href={`/orgs/${org.id}`} key={org.id} onClick={() => setOpen(false)} role="menuitem">
              <span className="truncate">{org.name}</span><PlanBadge plan={org.plan} />
            </Link>
          ))}
          <div className="my-1 border-t border-line" />
          <Link className="block rounded-md px-2.5 py-2 text-[13px] text-brand-700 hover:bg-brand-50" href="/orgs" onClick={() => setOpen(false)} role="menuitem">All organizations</Link>
        </div>
      ) : null}
    </div>
  );
}

function Sidebar({ organization, orgs, close }: { organization: OrganizationSummary; orgs: OrganizationSummary[]; close?: () => void }) {
  const pathname = usePathname();
  const base = `/orgs/${organization.id}`;
  return (
    <div className="flex h-full flex-col px-4 py-4">
      <div className="px-2"><Logo /></div>
      <OrgSwitcher organization={organization} orgs={orgs} />
      <nav aria-label="Primary" className="mt-5 space-y-1">
        {navigation.map((item) => {
          const href = item.segment === "report"
            ? organization.currentSeason ? `${base}/seasons/${organization.currentSeason.id}/report` : base
            : item.segment ? `${base}/${item.segment}` : base;
          const active = item.segment === ""
            ? pathname === base
            : item.segment === "report" ? pathname.includes("/report") : pathname.startsWith(`${base}/${item.segment}`);
          const Icon = item.icon;
          return (
            <Link
              aria-current={active ? "page" : undefined}
              className={cx(
                "relative flex h-9 items-center gap-3 rounded-md px-3 text-[13px] font-medium text-ink-soft hover:bg-sunken",
                active && "bg-brand-50 text-brand-700 before:absolute before:left-0 before:h-5 before:w-0.5 before:bg-brand-600",
              )}
              href={href}
              key={item.label}
              onClick={close}
            >
              <Icon aria-hidden="true" className="h-4 w-4" />
              <span>{item.label}</span>
              {item.segment === "report" && organization.plan === "free" ? <Lock aria-label="Pro feature" className="ml-auto h-3.5 w-3.5 text-ink-faint" /> : null}
            </Link>
          );
        })}
      </nav>
      <div className="mt-auto border-t border-line px-2 pt-4">
        <div className="flex items-center justify-between"><PlanBadge plan={organization.plan} />{organization.plan === "free" && organization.role === "owner" ? <Link className="text-[13px] font-medium text-brand-700 hover:underline" href={`${base}/billing`} onClick={close}>Upgrade</Link> : null}</div>
      </div>
    </div>
  );
}

function UserMenu({ organization }: { organization: OrganizationSummary }) {
  const { user, signOut } = useAuth();
  const [open, setOpen] = useState(false);
  const wrapper = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const close = (event: MouseEvent) => !wrapper.current?.contains(event.target as Node) && setOpen(false);
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, []);
  if (!user) return null;
  return (
    <div className="relative" ref={wrapper}>
      <button aria-expanded={open} aria-haspopup="menu" className="flex h-10 items-center gap-2 rounded-md px-1.5 hover:bg-sunken" onClick={() => setOpen((value) => !value)}>
        <span className="grid h-7 w-7 place-items-center rounded-full bg-brand-100 text-xs font-semibold text-brand-800">{initials(user.displayName)}</span>
        <span className="hidden max-w-40 truncate text-[13px] font-medium text-ink lg:block">{user.displayName}</span>
        <ChevronDown aria-hidden="true" className="h-3.5 w-3.5 text-ink-faint" />
      </button>
      {open ? (
        <div className="absolute right-0 top-11 z-40 w-64 rounded-lg border border-line bg-surface p-1.5 shadow-pop" onKeyDown={menuKeyDown} role="menu">
          <div className="px-2.5 py-2"><p className="truncate text-[13px] text-ink-soft">{user.email}</p><div className="mt-2"><RoleBadge role={organization.role} /></div></div>
          <div className="my-1 border-t border-line" />
          <button className="flex w-full items-center gap-2 rounded-md px-2.5 py-2 text-left text-[13px] text-ink-soft hover:bg-sunken" onClick={signOut} role="menuitem"><LogOut aria-hidden="true" className="h-4 w-4" />Sign out</button>
        </div>
      ) : null}
    </div>
  );
}

function ShellContent({ children }: { children: ReactNode }) {
  const { orgId } = useParams<{ orgId: string }>();
  const [mobileOpen, setMobileOpen] = useState(false);
  const pathname = usePathname();
  const orgQuery = useQuery({ queryKey: queryKeys.orgs, queryFn: () => apiFetch<OrganizationsResponse>("/api/orgs") });
  if (orgQuery.isLoading) return <AppSkeleton />;
  const organization = orgQuery.data?.organizations.find((org) => org.id === orgId);
  if (!organization) return <div className="grid min-h-screen place-items-center p-6"><div className="card max-w-md"><p className="font-display text-xl">Organization unavailable</p><p className="mt-2 text-sm text-ink-soft">It may not exist, or your membership no longer grants access.</p><Link className="mt-4 inline-block text-sm font-medium text-brand-700" href="/orgs">Return to organizations</Link></div></div>;
  const page = pathname.includes("/fields/") ? "Field detail" : pathname.endsWith("/fields") ? "Fields" : pathname.endsWith("/insights") ? "Insights" : pathname.includes("/report") ? "Season report" : pathname.endsWith("/members") ? "Members" : pathname.endsWith("/billing") ? "Plan & billing" : "Dashboard";
  return (
    <OrgProvider organization={organization}>
      <aside className="app-sidebar fixed inset-y-0 left-0 z-40 hidden w-[260px] border-r border-line bg-paper lg:block"><Sidebar organization={organization} orgs={orgQuery.data?.organizations ?? []} /></aside>
      {mobileOpen ? <div className="fixed inset-0 z-50 bg-ink/40 lg:hidden" onMouseDown={(event) => event.target === event.currentTarget && setMobileOpen(false)}><aside className="h-full w-[min(300px,calc(100vw-48px))] bg-paper shadow-modal"><button aria-label="Close navigation" className="absolute left-[min(252px,calc(100vw-96px))] top-4 rounded-md p-2 text-ink-soft hover:bg-sunken" onClick={() => setMobileOpen(false)}><X className="h-5 w-5" /></button><Sidebar close={() => setMobileOpen(false)} organization={organization} orgs={orgQuery.data?.organizations ?? []} /></aside></div> : null}
      <div className="min-h-screen lg:pl-[260px]">
        <header className="app-topbar sticky top-0 z-30 h-14 border-b border-line bg-surface">
          <div className="flex h-full items-center justify-between gap-4 px-4 sm:px-6">
            <div className="flex min-w-0 items-center gap-3"><button aria-label="Open navigation" className="rounded-md p-2 text-ink-soft hover:bg-sunken lg:hidden" onClick={() => setMobileOpen(true)}><Menu aria-hidden="true" className="h-5 w-5" /></button><p className="truncate text-[13px] text-ink-soft"><span className="text-ink">{organization.name}</span><span className="mx-2 text-line-strong">/</span>{page}</p></div>
            <UserMenu organization={organization} />
          </div>
        </header>
        <main className="app-main px-4 py-6 sm:px-6"><div className="mx-auto max-w-[1200px]">{children}</div></main>
      </div>
    </OrgProvider>
  );
}

export function AppShell({ children }: { children: ReactNode }) {
  return <RequireAuth><ShellContent>{children}</ShellContent></RequireAuth>;
}
