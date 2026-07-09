import { cx } from "@/lib/format";

export function ContourPattern({ className }: { className?: string }) {
  return (
    <svg
      aria-hidden="true"
      className={cx("pointer-events-none absolute inset-0 h-full w-full", className)}
      preserveAspectRatio="xMidYMid slice"
      viewBox="0 0 800 600"
    >
      <g fill="none" stroke="currentColor" strokeWidth="1.25">
        <path d="M-60 88C87 8 185 163 338 78s287-54 537 57" />
        <path d="M-51 124C96 44 193 200 346 114s288-54 538 57" />
        <path d="M-72 360c121-92 237-6 359-73s246-104 576 44" />
        <path d="M-81 397c121-92 237-6 359-73s246-104 576 44" />
        <path d="M119-47c-58 123 85 155 35 285S48 432 121 656" />
        <path d="M166-55c-58 123 85 155 35 285S95 424 168 648" />
        <path d="M544-54c-83 95-7 181-73 264s-9 236 85 445" />
        <path d="M589-57c-83 95-7 181-73 264s-9 236 85 445" />
        <ellipse cx="388" cy="293" rx="101" ry="68" />
        <ellipse cx="388" cy="293" rx="70" ry="43" />
      </g>
    </svg>
  );
}
