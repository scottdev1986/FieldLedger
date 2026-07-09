"use client";

import { CircleCheck, CircleX, Info, X } from "lucide-react";
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { cx } from "@/lib/format";

type ToastType = "success" | "danger" | "info";
type ToastInput = { type?: ToastType; title: string; description?: string };
type ToastRecord = ToastInput & { id: number; type: ToastType };

const ToastContext = createContext<{ toast: (input: ToastInput) => void } | null>(null);

function ToastItem({ item, dismiss }: { item: ToastRecord; dismiss: () => void }) {
  useEffect(() => {
    if (item.type === "danger") return;
    const timer = window.setTimeout(dismiss, 5000);
    return () => window.clearTimeout(timer);
  }, [dismiss, item.type]);

  const Icon = item.type === "success" ? CircleCheck : item.type === "danger" ? CircleX : Info;
  return (
    <div
      className={cx("toast flex w-[min(360px,calc(100vw-32px))] gap-3", `toast-${item.type}`)}
      role={item.type === "danger" ? "alert" : "status"}
    >
      <Icon aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0" />
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium text-ink">{item.title}</p>
        {item.description ? <p className="mt-0.5 text-[13px] text-ink-soft">{item.description}</p> : null}
      </div>
      <button aria-label="Dismiss notification" className="-mr-1 -mt-1 rounded p-1 text-ink-faint hover:bg-sunken" onClick={dismiss}>
        <X aria-hidden="true" className="h-4 w-4" />
      </button>
    </div>
  );
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastRecord[]>([]);
  const toast = useCallback((input: ToastInput) => {
    setItems((current) => [...current.slice(-2), { ...input, type: input.type ?? "info", id: Date.now() + Math.random() }]);
  }, []);
  const value = useMemo(() => ({ toast }), [toast]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="fixed bottom-4 right-4 z-[100] flex flex-col items-end gap-2">
        {items.map((item) => (
          <ToastItem key={item.id} item={item} dismiss={() => setItems((all) => all.filter((entry) => entry.id !== item.id))} />
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const context = useContext(ToastContext);
  if (!context) throw new Error("useToast must be used inside ToastProvider");
  return context;
}
