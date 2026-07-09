export type Role = "owner" | "agronomist" | "viewer";
export type Plan = "free" | "pro";
export type FieldStatus = "active" | "archived";
export type ActivityType =
  | "planting"
  | "spraying"
  | "irrigation"
  | "fertilizer"
  | "harvest"
  | "note";

export type ApiErrorBody = {
  error: {
    code: string;
    message: string;
    traceId: string;
    fieldErrors?: Record<string, string[]>;
  };
};

export type User = {
  id: string;
  email: string;
  displayName: string;
  createdAt: string;
};

export type SeasonSummary = {
  id: string;
  year: number;
  name: string;
  startsOn: string;
  endsOn: string;
};

export type Membership = {
  organizationId: string;
  organizationName: string;
  organizationSlug: string;
  role: Role;
  plan: Plan;
};

export type AuthSessionResponse = {
  accessToken: string;
  user: User;
};

export type MeResponse = {
  user: User;
  memberships: Membership[];
};

export type OrganizationSummary = {
  id: string;
  name: string;
  slug: string;
  role: Role;
  plan: Plan;
  activeFieldCount: number;
  seasonCount: number;
  currentSeason: SeasonSummary | null;
};

export type OrganizationsResponse = {
  organizations: OrganizationSummary[];
};

export type OrganizationResponse = {
  organization: OrganizationSummary;
};

export type Activity = {
  id: string;
  organizationId: string;
  fieldId: string;
  fieldName: string;
  fieldAcreage: number;
  seasonId: string;
  seasonName: string;
  type: ActivityType;
  activityDate: string;
  quantity: number | null;
  quantityUnit: string | null;
  costAmount: number | null;
  revenueAmount: number | null;
  notes: string | null;
  createdBy: {
    id: string;
    displayName: string;
  };
  createdAt: string;
};

export type DashboardResponse = {
  organization: OrganizationSummary;
  currentSeason: SeasonSummary | null;
  metrics: {
    activeFieldCount: number;
    totalAcreage: number;
    seasonProgressPercent: number | null;
    activitiesThisSeason: number;
    inputCost: number;
    harvestValue: number;
    netValue: number;
    yieldPerAcre: number | null;
  };
  recentActivities: Activity[];
  cropProgress: Array<{
    crop: string;
    acreage: number;
    fieldCount: number;
    daysToHarvest: number | null;
  }>;
  limits: {
    maxFields: number | null;
  };
};

export type FieldListItem = {
  id: string;
  name: string;
  acreage: number;
  defaultCrop: string;
  soilType: string | null;
  status: FieldStatus;
  currentCrop: string | null;
  lastActivity: {
    type: ActivityType;
    activityDate: string;
  } | null;
};

export type FieldsResponse = {
  fields: FieldListItem[];
};

export type FieldSeasonRollup = {
  seasonId: string;
  seasonName: string;
  year: number;
  crop: string;
  plantedOn: string | null;
  yieldPerAcre: number | null;
  inputCost: number;
  harvestValue: number;
  netValue: number;
  priorYieldDeltaPercent: number | null;
};

export type FieldResponse = {
  field: FieldListItem;
  seasonRollups: FieldSeasonRollup[];
};

export type FieldInput = {
  name: string;
  acreage: number;
  defaultCrop: string;
  soilType: string | null;
};

export type SeasonInput = {
  year: number;
  name: string;
  startsOn: string;
  endsOn: string;
};

export type SeasonResponse = {
  season: SeasonSummary;
};

export type SeasonsResponse = {
  seasons: SeasonSummary[];
};

export type ActivityInput = {
  seasonId: string;
  type: ActivityType;
  activityDate: string;
  quantity: number | null;
  quantityUnit: string | null;
  costAmount: number | null;
  revenueAmount: number | null;
  notes: string | null;
};

export type ActivitiesResponse = {
  activities: Activity[];
};

export type ActivityResponse = {
  activity: Activity;
};

export type InsightsResponse = {
  selectedSeasonId: string;
  totals: {
    activeFields: number;
    totalAcreage: number;
    inputCost: number;
    harvestValue: number;
    netValue: number;
  };
  yieldBySeason: Array<{
    year: number;
    crop: string;
    yieldPerAcre: number;
  }>;
  costVsValue: Array<{
    year: number;
    inputCost: number;
    harvestValue: number;
  }>;
  cropMix: Array<{
    crop: string;
    acreage: number;
  }>;
  fieldNetValue: Array<{
    fieldId: string;
    fieldName: string;
    netValue: number;
  }>;
  activityCountByType: Array<{
    type: ActivityType;
    count: number;
  }>;
};

export type ReportResponse = {
  organization: {
    id: string;
    name: string;
    plan: Plan;
  };
  season: SeasonSummary;
  generatedAt: string;
  summary: {
    activeFields: number;
    totalAcreage: number;
    inputCost: number;
    harvestValue: number;
    netValue: number;
    averageYieldPerAcre: number | null;
  };
  fields: Array<{
    fieldId: string;
    fieldName: string;
    crop: string;
    acreage: number;
    activityCount: number;
    inputCost: number;
    harvestValue: number;
    yieldPerAcre: number | null;
  }>;
  activitySummary: Array<{
    type: ActivityType;
    count: number;
  }>;
  activities: Activity[];
};

export type Member = {
  userId: string;
  displayName: string;
  email: string;
  role: Role;
  joinedAt: string;
};

export type MembersResponse = {
  members: Member[];
};

export type MemberResponse = {
  member: Member;
};

export type InviteMemberInput = {
  email: string;
  role: Role;
};

export type BillingResponse = {
  plan: Plan;
  limits: {
    maxFields: number | null;
    csvExportEnabled: boolean;
    seasonReportEnabled: boolean;
  };
  usage: {
    activeFieldCount: number;
  };
  history: Array<{
    fromPlan: Plan;
    toPlan: Plan;
    changedBy: string;
    changedAt: string;
  }>;
};

export type PlanMutationResponse = {
  plan: Plan;
  changedAt: string;
};
