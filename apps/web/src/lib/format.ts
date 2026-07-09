export const formatNumber = (value: number, maximumFractionDigits = 1) =>
  new Intl.NumberFormat("en-US", { maximumFractionDigits }).format(value);

export const formatCurrency = (value: number, compact = false) =>
  new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: compact ? 0 : 2,
    notation: compact ? "compact" : "standard",
  }).format(value);

export const formatDate = (value: string, options?: Intl.DateTimeFormatOptions) =>
  new Intl.DateTimeFormat("en-US", options ?? { month: "short", day: "numeric", year: "numeric" }).format(
    new Date(value.includes("T") ? value : `${value}T12:00:00`),
  );

export const initials = (name: string) =>
  name
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");

export const titleCase = (value: string) => value.charAt(0).toUpperCase() + value.slice(1);

export const cx = (...values: Array<string | false | null | undefined>) => values.filter(Boolean).join(" ");
