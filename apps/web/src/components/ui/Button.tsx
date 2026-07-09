import { LoaderCircle } from "lucide-react";
import { forwardRef, type ButtonHTMLAttributes } from "react";
import { cx } from "@/lib/format";

export type ButtonVariant = "primary" | "secondary" | "ghost" | "destructive";
export type ButtonSize = "sm" | "md" | "lg";

export function buttonClasses(variant: ButtonVariant = "primary", size: ButtonSize = "md") {
  return cx(
    "inline-flex shrink-0 items-center justify-center gap-2 rounded-md font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50",
    size === "sm" && "h-8 px-3 text-[13px]",
    size === "md" && "h-10 px-4 text-sm",
    size === "lg" && "h-11 px-5 text-sm",
    variant === "primary" && "bg-brand-600 text-white hover:bg-brand-700 active:bg-brand-800",
    variant === "secondary" && "border border-line-strong bg-surface text-ink hover:bg-sunken",
    variant === "ghost" && "text-ink-soft hover:bg-sunken hover:text-ink",
    variant === "destructive" && "bg-danger text-white hover:brightness-90",
  );
}

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
  size?: ButtonSize;
  loading?: boolean;
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { className, variant = "primary", size = "md", loading, children, disabled, ...props },
  ref,
) {
  return (
    <button
      ref={ref}
      className={cx(buttonClasses(variant, size), className)}
      disabled={disabled || loading}
      {...props}
    >
      {loading ? <LoaderCircle aria-hidden="true" className="h-4 w-4 animate-spin" /> : null}
      {children}
    </button>
  );
});
