export const queryKeys = {
  orgs: ["orgs"] as const,
  org: (orgId: string) => ["orgs", orgId] as const,
  dashboard: (orgId: string) => ["orgs", orgId, "dashboard"] as const,
  fields: (orgId: string) => ["orgs", orgId, "fields"] as const,
  field: (orgId: string, fieldId: string) => ["orgs", orgId, "fields", fieldId] as const,
  seasons: (orgId: string) => ["orgs", orgId, "seasons"] as const,
  activities: (orgId: string, fieldId: string, seasonId?: string) =>
    ["orgs", orgId, "fields", fieldId, "activities", seasonId ?? "all"] as const,
  insights: (orgId: string) => ["orgs", orgId, "insights"] as const,
  report: (orgId: string, seasonId: string) => ["orgs", orgId, "reports", seasonId] as const,
  members: (orgId: string) => ["orgs", orgId, "members"] as const,
  billing: (orgId: string) => ["orgs", orgId, "billing"] as const,
};
