"use client";

import { createContext, useContext, type ReactNode } from "react";
import type { OrganizationSummary } from "@/lib/api-types";

const OrgContext = createContext<OrganizationSummary | null>(null);

export function OrgProvider({ organization, children }: { organization: OrganizationSummary; children: ReactNode }) {
  return <OrgContext.Provider value={organization}>{children}</OrgContext.Provider>;
}

export function useCurrentOrg() {
  const org = useContext(OrgContext);
  if (!org) throw new Error("useCurrentOrg must be used inside OrgProvider");
  return org;
}
