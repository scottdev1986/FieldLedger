import type { ActivityType, Plan, Role } from "@/lib/api-types";
import { cx, titleCase } from "@/lib/format";

const base = "inline-flex h-[22px] items-center gap-1.5 whitespace-nowrap rounded-full px-2 text-[11px] font-medium";

export function PlanBadge({ plan }: { plan: Plan }) {
  return (
    <span
      className={cx(
        base,
        "uppercase tracking-wide",
        plan === "pro" ? "bg-ink text-white" : "border border-line-strong bg-surface text-ink-soft",
      )}
    >
      {plan}
    </span>
  );
}

export function RoleBadge({ role }: { role: Role }) {
  return (
    <span
      className={cx(
        base,
        "border uppercase tracking-wide",
        role === "owner" && "border-warn/25 bg-warn-bg text-warn",
        role === "agronomist" && "border-brand-200 bg-brand-50 text-brand-700",
        role === "viewer" && "border-info/25 bg-info-bg text-info",
      )}
    >
      {role}
    </span>
  );
}

export function ActivityBadge({ type }: { type: ActivityType }) {
  return (
    <span className={cx(base, "activity-badge border", `activity-${type}`)}>
      <span aria-hidden="true" className="activity-dot h-1.5 w-1.5 rounded-full" />
      {titleCase(type)}
    </span>
  );
}

export function StatusBadge({ status }: { status: "active" | "archived" }) {
  return (
    <span
      className={cx(
        base,
        "border",
        status === "active"
          ? "border-success/20 bg-success-bg text-success"
          : "border-line-strong bg-sunken text-ink-soft",
      )}
    >
      {titleCase(status)}
    </span>
  );
}
