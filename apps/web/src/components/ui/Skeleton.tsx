import { cx } from "@/lib/format";

export function Skeleton({ className }: { className?: string }) {
  return <div aria-hidden="true" className={cx("animate-pulse rounded-sm bg-sunken", className)} />;
}

export function AppSkeleton() {
  return (
    <div className="min-h-screen bg-paper p-4 lg:pl-[284px] lg:pt-20">
      <div className="mx-auto max-w-[1200px] space-y-6">
        <div className="space-y-3"><Skeleton className="h-3 w-28" /><Skeleton className="h-9 w-64" /></div>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          {[0, 1, 2, 3].map((item) => <div className="card h-28" key={item}><Skeleton className="h-3 w-24" /><Skeleton className="mt-5 h-8 w-32" /></div>)}
        </div>
        <div className="card h-80"><Skeleton className="h-full w-full" /></div>
      </div>
    </div>
  );
}
