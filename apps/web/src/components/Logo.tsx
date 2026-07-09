import { Sprout } from "lucide-react";
import { cx } from "@/lib/format";

export function Logo({ inverse = false, className }: { inverse?: boolean; className?: string }) {
  return (
    <span className={cx("inline-flex items-center gap-2 text-[15px] font-semibold tracking-tight", inverse ? "text-white" : "text-ink", className)}>
      <span className="grid h-6 w-6 shrink-0 place-items-center rounded-md bg-brand-600 text-white shadow-xs">
        <Sprout aria-hidden="true" className="h-4 w-4" strokeWidth={2} />
      </span>
      FieldLedger
    </span>
  );
}
