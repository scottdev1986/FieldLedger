import { CircleAlert, FolderOpen, Lock, type LucideIcon } from "lucide-react";
import Link from "next/link";
import type { HTMLAttributes, ReactNode } from "react";
import { buttonClasses } from "@/components/ui/Button";
import type { Role } from "@/lib/api-types";
import { cx } from "@/lib/format";

export function Card({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx("card", className)} {...props} />;
}

export function Overline({ children, className }: { children: ReactNode; className?: string }) {
  return <p className={cx("overline", className)}>{children}</p>;
}

export function PageHeader({
  overline,
  title,
  description,
  action,
}: {
  overline: string;
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <header className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div className="min-w-0">
        <Overline>{overline}</Overline>
        <h1 className="mt-2 text-2xl font-semibold leading-tight tracking-tight text-ink">{title}</h1>
        {description ? <p className="mt-2 max-w-2xl text-sm text-ink-soft">{description}</p> : null}
      </div>
      {action ? <div className="flex shrink-0 items-center gap-2">{action}</div> : null}
    </header>
  );
}

export function FieldError({ id, message }: { id: string; message?: string }) {
  if (!message) return null;
  return <p className="mt-1.5 flex items-center gap-1 text-xs text-danger" id={id}><CircleAlert aria-hidden="true" className="h-3.5 w-3.5" />{message}</p>;
}

export function EmptyState({
  title,
  description,
  action,
  icon: Icon = FolderOpen,
}: {
  title: string;
  description: string;
  action?: ReactNode;
  icon?: LucideIcon;
}) {
  return (
    <div className="flex min-h-64 flex-col items-center justify-center px-6 py-12 text-center">
      <span className="grid h-11 w-11 place-items-center rounded-lg border border-line bg-sunken">
        <Icon aria-hidden="true" className="h-5 w-5 text-ink-faint" />
      </span>
      <p className="mt-4 text-[15px] font-semibold text-ink">{title}</p>
      <p className="mt-1 max-w-sm text-[13px] text-ink-soft">{description}</p>
      {action ? <div className="mt-5">{action}</div> : null}
    </div>
  );
}

export function ProLock({ orgId, role, sentence }: { orgId: string; role: Role; sentence: string }) {
  return (
    <Card>
      <div className="flex items-center gap-2 text-brand-700"><Lock aria-hidden="true" className="h-4 w-4" /><Overline className="text-brand-700">Pro</Overline></div>
      <p className="mt-3 text-sm text-ink">{sentence}</p>
      {role === "owner" ? (
        <Link className={cx(buttonClasses("primary", "sm"), "mt-4")} href={`/orgs/${orgId}/billing`}>Upgrade to Pro</Link>
      ) : <p className="mt-3 text-[13px] text-ink-soft">Ask an owner to upgrade.</p>}
    </Card>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const message = error instanceof Error ? error.message : "Something went wrong while loading this page.";
  return (
    <Card className="border-danger/25 bg-danger-bg">
      <div className="flex gap-3"><CircleAlert aria-hidden="true" className="mt-0.5 h-5 w-5 text-danger" /><div><p className="font-medium text-ink">Unable to load data</p><p className="mt-1 text-[13px] text-ink-soft">{message}</p>{onRetry ? <button className="mt-3 text-[13px] font-medium text-brand-700 hover:underline" onClick={onRetry}>Try again</button> : null}</div></div>
    </Card>
  );
}
