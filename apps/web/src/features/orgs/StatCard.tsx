import { Card, Overline } from "@/components/ui/Primitives";

export function StatCard({ label, value, unit, context }: { label: string; value: string; unit?: string; context?: string }) {
  return <Card><Overline>{label}</Overline><p className="mt-4 font-mono text-[28px] font-medium leading-none text-ink tabular-nums">{value}{unit ? <span className="ml-1.5 text-base font-normal text-ink-soft">{unit}</span> : null}</p>{context ? <p className="mt-3 text-xs text-ink-soft">{context}</p> : null}</Card>;
}
