"use client";

import { MoreHorizontal, Pencil, Trash2 } from "lucide-react";
import { useMemo, useState } from "react";
import { ActivityBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import type { Activity } from "@/lib/api-types";
import { formatCurrency, formatDate, formatNumber } from "@/lib/format";

const accent: Record<Activity["type"], string> = {
  planting: "bg-act-planting", spraying: "bg-act-spraying", irrigation: "bg-act-irrigation",
  fertilizer: "bg-act-fertilizer", harvest: "bg-act-harvest", note: "bg-act-note",
};

export function ActivityTimeline({ activities, editable = false, onEdit, onDelete }: { activities: Activity[]; editable?: boolean; onEdit?: (activity: Activity) => void; onDelete?: (activity: Activity) => void }) {
  const [openId, setOpenId] = useState<string | null>(null);
  const grouped = useMemo(() => {
    const groups = new Map<string, Activity[]>();
    activities.forEach((activity) => {
      const key = new Intl.DateTimeFormat("en-US", { month: "long", year: "numeric" }).format(new Date(`${activity.activityDate}T12:00:00`)).toUpperCase();
      groups.set(key, [...(groups.get(key) ?? []), activity]);
    });
    return [...groups.entries()];
  }, [activities]);
  return (
    <div className="space-y-5">
      {grouped.map(([month, items]) => (
        <section key={month}>
          <p className="overline sticky top-14 z-10 -mx-1 bg-surface/95 px-1 py-2">{month}</p>
          <div className="ml-1.5 border-l border-line pl-5">
            {items.map((activity) => {
              const financials = [activity.costAmount ? `${formatCurrency(activity.costAmount)} cost` : null, activity.revenueAmount ? `${formatCurrency(activity.revenueAmount)} revenue` : null].filter(Boolean).join(" · ");
              return (
                <article className="group relative pb-5 last:pb-0" key={activity.id}>
                  <span aria-hidden="true" className={`absolute -left-[26px] top-2 h-2.5 w-2.5 rounded-full ring-2 ring-paper ${accent[activity.type]}`} />
                  <div className="flex min-w-0 items-start gap-2 pr-8"><ActivityBadge type={activity.type} /><p className="truncate text-sm font-medium text-ink">{activity.fieldName}</p></div>
                  <p className="mt-1 text-[13px] text-ink-soft">{activity.notes || financials || (activity.quantity !== null ? `${formatNumber(activity.quantity)} ${activity.quantityUnit ?? "units"}` : "Activity logged")}</p>
                  <p className="mt-1 font-mono text-xs text-ink-faint"><time>{formatDate(activity.activityDate)}</time><span className="mx-2">·</span>{formatNumber(activity.fieldAcreage)} ac</p>
                  {editable ? <div className="absolute right-0 top-0"><Button aria-label={`Actions for ${activity.type}`} className="opacity-0 group-focus-within:opacity-100 group-hover:opacity-100" onClick={() => setOpenId(openId === activity.id ? null : activity.id)} size="sm" variant="ghost"><MoreHorizontal className="h-4 w-4" /></Button>{openId === activity.id ? <div className="absolute right-0 top-9 z-20 w-32 rounded-md border border-line bg-surface p-1 shadow-pop"><button className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-[13px] hover:bg-sunken" onClick={() => { setOpenId(null); onEdit?.(activity); }}><Pencil className="h-3.5 w-3.5" />Edit</button><button className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-[13px] text-danger hover:bg-danger-bg" onClick={() => { setOpenId(null); onDelete?.(activity); }}><Trash2 className="h-3.5 w-3.5" />Delete</button></div> : null}</div> : null}
                </article>
              );
            })}
          </div>
        </section>
      ))}
    </div>
  );
}
