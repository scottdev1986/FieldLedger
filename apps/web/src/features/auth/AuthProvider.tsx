"use client";

import { useRouter } from "next/navigation";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { apiFetch } from "@/lib/api-client";
import type { AuthSessionResponse, MeResponse, Membership, User } from "@/lib/api-types";
import { clearAccessToken, hydrateAccessToken, persistAccessToken } from "@/lib/auth";

type AuthStatus = "loading" | "authenticated" | "unauthenticated";

type AuthContextValue = {
  status: AuthStatus;
  user: User | null;
  memberships: Membership[];
  establishSession: (session: AuthSessionResponse) => Promise<void>;
  refreshMe: () => Promise<void>;
  signOut: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>("loading");
  const [user, setUser] = useState<User | null>(null);
  const [memberships, setMemberships] = useState<Membership[]>([]);
  const router = useRouter();

  const clearSession = useCallback(() => {
    clearAccessToken();
    setUser(null);
    setMemberships([]);
    setStatus("unauthenticated");
  }, []);

  const refreshMe = useCallback(async () => {
    const me = await apiFetch<MeResponse>("/api/auth/me");
    setUser(me.user);
    setMemberships(me.memberships);
    setStatus("authenticated");
  }, []);

  useEffect(() => {
    const token = hydrateAccessToken();
    if (!token) {
      queueMicrotask(() => setStatus("unauthenticated"));
      return;
    }
    queueMicrotask(() => void refreshMe().catch(clearSession));
  }, [clearSession, refreshMe]);

  useEffect(() => {
    window.addEventListener("fieldledger:unauthorized", clearSession);
    window.addEventListener("fieldledger:session-cleared", clearSession);
    return () => {
      window.removeEventListener("fieldledger:unauthorized", clearSession);
      window.removeEventListener("fieldledger:session-cleared", clearSession);
    };
  }, [clearSession]);

  const establishSession = useCallback(
    async (session: AuthSessionResponse) => {
      persistAccessToken(session.accessToken);
      setUser(session.user);
      setStatus("authenticated");
      try {
        await refreshMe();
      } catch {
        clearSession();
        throw new Error("We could not finish loading your account.");
      }
    },
    [clearSession, refreshMe],
  );

  const signOut = useCallback(() => {
    clearSession();
    router.replace("/login");
  }, [clearSession, router]);

  const value = useMemo(
    () => ({ status, user, memberships, establishSession, refreshMe, signOut }),
    [status, user, memberships, establishSession, refreshMe, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used inside AuthProvider");
  return context;
}
