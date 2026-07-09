"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Logo } from "@/components/Logo";
import { RoleBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { FieldError } from "@/components/ui/Primitives";
import { AuthFrame } from "@/features/auth/AuthFrame";
import { useAuth } from "@/features/auth/AuthProvider";
import { apiFetch } from "@/lib/api-client";
import type { AuthSessionResponse, Role } from "@/lib/api-types";

const schema = z.object({ email: z.string().email("Enter a valid email address."), password: z.string().min(1, "Enter your password.") });
type LoginValues = z.infer<typeof schema>;
const demoAccounts: Array<{ role: Role; email: string }> = [
  { role: "owner", email: "owner@fieldledger.demo" },
  { role: "agronomist", email: "agronomist@fieldledger.demo" },
  { role: "viewer", email: "viewer@fieldledger.demo" },
];

export default function LoginPage() {
  const { establishSession } = useAuth();
  const router = useRouter();
  const [serverError, setServerError] = useState<string>();
  const [demoLoading, setDemoLoading] = useState<Role | null>(null);
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<LoginValues>({ resolver: zodResolver(schema), defaultValues: { email: "", password: "" } });

  const signIn = async (values: LoginValues) => {
    setServerError(undefined);
    try {
      const session = await apiFetch<AuthSessionResponse>("/api/auth/login", { method: "POST", body: JSON.stringify(values) });
      await establishSession(session);
      router.replace("/orgs");
    } catch (error) {
      setServerError(error instanceof Error ? error.message : "Sign in failed. Check your details and try again.");
    }
  };

  const signInDemo = async (role: Role, email: string) => {
    setDemoLoading(role);
    await signIn({ email, password: "FieldLedgerDemo!2026" });
    setDemoLoading(null);
  };

  return (
    <AuthFrame>
      <div className="mb-9 min-[900px]:hidden"><Logo /></div>
      <p className="overline">Welcome back</p>
      <h2 className="mt-2 font-display text-[28px] font-semibold text-ink">Sign in to your ledger</h2>
      <p className="mt-2 text-sm text-ink-soft">Use your account or step into a seeded demo role.</p>
      <form className="mt-7 space-y-4" onSubmit={handleSubmit(signIn)}>
        <div><label className="form-label" htmlFor="email">Email</label><input autoComplete="email" className="form-control" id="email" type="email" aria-invalid={Boolean(errors.email)} aria-describedby={errors.email ? "email-error" : undefined} {...register("email")} /><FieldError id="email-error" message={errors.email?.message} /></div>
        <div><label className="form-label" htmlFor="password">Password</label><input autoComplete="current-password" className="form-control" id="password" type="password" aria-invalid={Boolean(errors.password)} aria-describedby={errors.password ? "password-error" : undefined} {...register("password")} /><FieldError id="password-error" message={errors.password?.message} /></div>
        {serverError ? <p className="rounded-md border border-danger/20 bg-danger-bg p-3 text-[13px] text-danger" role="alert">{serverError}</p> : null}
        <Button className="w-full" loading={isSubmitting} type="submit">Sign in</Button>
      </form>
      <div className="my-6 flex items-center gap-3"><span className="h-px flex-1 bg-line" /><span className="overline">Demo accounts</span><span className="h-px flex-1 bg-line" /></div>
      <div className="space-y-2">
        {demoAccounts.map((account) => <Button className="w-full justify-between" disabled={Boolean(demoLoading)} key={account.role} loading={demoLoading === account.role} onClick={() => void signInDemo(account.role, account.email)} variant="secondary"><span>Sign in as {account.role === "agronomist" ? "Agronomist" : account.role === "owner" ? "Owner" : "Viewer"}</span><RoleBadge role={account.role} /></Button>)}
      </div>
      <p className="mt-6 text-center text-[13px] text-ink-soft">New to FieldLedger? <Link className="font-medium text-brand-700 hover:underline" href="/register">Create an account</Link></p>
      <p className="mt-8 text-center text-xs text-ink-faint">Demo environment — data resets on reseed.</p>
    </AuthFrame>
  );
}
