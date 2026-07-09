import { ContourPattern } from "@/components/ContourPattern";
import { Logo } from "@/components/Logo";

export function AuthFrame({ children }: { children: React.ReactNode }) {
  return (
    <main className="grid min-h-screen bg-paper min-[900px]:grid-cols-[45%_55%]">
      <section className="relative isolate hidden overflow-hidden bg-brand-800 p-10 text-white min-[900px]:flex min-[900px]:flex-col">
        <ContourPattern className="-z-10 text-brand-200 opacity-[0.08]" />
        <Logo inverse />
        <div className="my-auto max-w-md">
          <h1 className="font-display text-[32px] font-medium leading-[1.15]">The record book for every acre.</h1>
          <p className="mt-4 text-sm leading-6 text-brand-200">Field operations, seasonal performance, and the business of growing—kept in one credible ledger.</p>
        </div>
        <p className="text-xs text-brand-300">Built for the decisions between planting and harvest.</p>
      </section>
      <section className="flex min-h-screen items-center justify-center px-5 py-10 sm:px-8">
        <div className="w-full max-w-[380px]">{children}</div>
      </section>
    </main>
  );
}
