"use client";

import { X } from "lucide-react";
import { useEffect, useId, useRef, type ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { cx } from "@/lib/format";

type DialogProps = {
  open: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
  footer?: ReactNode;
  variant?: "drawer" | "modal";
  description?: string;
};

export function Dialog({ open, onClose, title, description, children, footer, variant = "drawer" }: DialogProps) {
  const titleId = useId();
  const descriptionId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const returnFocusRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;
    returnFocusRef.current = document.activeElement as HTMLElement;
    const dialog = dialogRef.current;
    const focusable = dialog?.querySelector<HTMLElement>("button, [href], input, select, textarea, [tabindex]:not([tabindex='-1'])");
    focusable?.focus();
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
      if (event.key !== "Tab" || !dialog) return;
      const elements = [...dialog.querySelectorAll<HTMLElement>("button, [href], input, select, textarea, [tabindex]:not([tabindex='-1'])")].filter(
        (element) => !element.hasAttribute("disabled"),
      );
      if (!elements.length) return;
      const first = elements[0];
      const last = elements[elements.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener("keydown", onKeyDown);
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.body.style.overflow = "";
      returnFocusRef.current?.focus();
    };
  }, [onClose, open]);

  if (!open) return null;
  return (
    <div className={cx("dialog-overlay", variant === "modal" && "items-center justify-center p-4")} onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <div
        aria-describedby={description ? descriptionId : undefined}
        aria-labelledby={titleId}
        aria-modal="true"
        className={cx("dialog-panel", variant === "drawer" ? "dialog-drawer" : "dialog-modal")}
        ref={dialogRef}
        role="dialog"
      >
        <header className="flex items-start justify-between gap-4 border-b border-line px-5 py-4">
          <div>
            <h2 className="text-base font-semibold tracking-tight text-ink" id={titleId}>{title}</h2>
            {description ? <p className="mt-1 text-[13px] text-ink-soft" id={descriptionId}>{description}</p> : null}
          </div>
          <Button aria-label="Close" className="-mr-2 -mt-2" onClick={onClose} size="sm" variant="ghost">
            <X aria-hidden="true" className="h-4 w-4" />
          </Button>
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto p-5">{children}</div>
        {footer ? <footer className="flex justify-end gap-2 border-t border-line bg-surface px-5 py-4">{footer}</footer> : null}
      </div>
    </div>
  );
}
