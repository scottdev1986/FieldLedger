import { Sprout } from "lucide-react";
import { cx } from "@/lib/format";

export function Logo({ inverse = false, className }: { inverse?: boolean; className?: string }) {
  return (
    <span className={cx("inline-flex items-center gap-2 font-display text-lg font-semibold", inverse ? "text-white" : "text-ink", className)}>
      <span className={cx("grid h-7 w-7 place-items-center rounded-md border", inverse ? "border-brand-400/50 text-brand-200" : "border-brand-200 bg-brand-50 text-brand-600")}>
        <Sprout aria-hidden="true" className="h-5 w-5" strokeWidth={1.8} />
      </span>
      FieldLedger
    </span>
  );
}
