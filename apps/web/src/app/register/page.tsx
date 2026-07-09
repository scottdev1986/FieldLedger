"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Logo } from "@/components/Logo";
import { Button } from "@/components/ui/Button";
import { FieldError } from "@/components/ui/Primitives";
import { AuthFrame } from "@/features/auth/AuthFrame";
import { useAuth } from "@/features/auth/AuthProvider";
import { apiFetch } from "@/lib/api-client";
import type { AuthSessionResponse } from "@/lib/api-types";

const schema = z.object({
  email: z.string().email("Enter a valid email address."),
  displayName: z.string().trim().min(2, "Enter at least two characters.").max(80),
  password: z.string().min(12, "Use at least 12 characters."),
});
type RegisterValues = z.infer<typeof schema>;

export default function RegisterPage() {
  const router = useRouter();
  const { establishSession } = useAuth();
  const [serverError, setServerError] = useState<string>();
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<RegisterValues>({ resolver: zodResolver(schema), defaultValues: { email: "", displayName: "", password: "" } });
  const submit = async (values: RegisterValues) => {
    setServerError(undefined);
    try {
      const session = await apiFetch<AuthSessionResponse>("/api/auth/register", { method: "POST", body: JSON.stringify(values) });
      await establishSession(session);
      router.replace("/orgs");
    } catch (error) { setServerError(error instanceof Error ? error.message : "Registration failed. Please try again."); }
  };
  return (
    <AuthFrame>
      <div className="mb-9 min-[900px]:hidden"><Logo /></div>
      <p className="overline">Start a ledger</p><h2 className="mt-2 font-display text-[28px] font-semibold">Create your account</h2><p className="mt-2 text-sm text-ink-soft">You can create or join a farm organization next.</p>
      <form className="mt-7 space-y-4" onSubmit={handleSubmit(submit)}>
        <div><label className="form-label" htmlFor="displayName">Display name</label><input autoComplete="name" className="form-control" id="displayName" aria-invalid={Boolean(errors.displayName)} aria-describedby={errors.displayName ? "name-error" : undefined} {...register("displayName")} /><FieldError id="name-error" message={errors.displayName?.message} /></div>
        <div><label className="form-label" htmlFor="email">Email</label><input autoComplete="email" className="form-control" id="email" type="email" aria-invalid={Boolean(errors.email)} aria-describedby={errors.email ? "email-error" : undefined} {...register("email")} /><FieldError id="email-error" message={errors.email?.message} /></div>
        <div><label className="form-label" htmlFor="password">Password</label><input autoComplete="new-password" className="form-control" id="password" type="password" aria-invalid={Boolean(errors.password)} aria-describedby={errors.password ? "password-error" : "password-help"} {...register("password")} /><p className="mt-1.5 text-xs text-ink-faint" id="password-help">At least 12 characters.</p><FieldError id="password-error" message={errors.password?.message} /></div>
        {serverError ? <p className="rounded-md border border-danger/20 bg-danger-bg p-3 text-[13px] text-danger" role="alert">{serverError}</p> : null}
        <Button className="w-full" loading={isSubmitting} type="submit">Create account</Button>
      </form>
      <p className="mt-6 text-center text-[13px] text-ink-soft">Already have an account? <Link className="font-medium text-brand-700 hover:underline" href="/login">Sign in</Link></p>
    </AuthFrame>
  );
}
