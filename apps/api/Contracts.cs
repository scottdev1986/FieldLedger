using System.Text.Json.Serialization;

namespace FieldLedger.Api;

public enum MemberRole { Owner, Agronomist, Viewer }
public enum PlanTier { Free, Pro }
public enum ActivityKind { Planting, Spraying, Irrigation, Fertilizer, Harvest, Note }
public enum FieldStatus { Active, Archived }

public sealed record HealthResponse(string Status, string Service, DateTimeOffset CheckedAt);

public sealed record AppUser(
    Guid Id,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedAt,
    [property: JsonIgnore] string PasswordHash = "");

public sealed record RegisterRequest(string? Email, string? DisplayName, string? Password);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record AuthResponse(string AccessToken, AppUser User);
public sealed record MeResponse(AppUser User, IReadOnlyList<MembershipResponse> Memberships);
public sealed record MembershipResponse(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    MemberRole Role,
    PlanTier Plan);

public sealed record OrganizationCreateRequest(string? Name, string? Slug);
public sealed record OrganizationUpdateRequest(string? Name, string? Slug);
public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string Slug,
    MemberRole Role,
    PlanTier Plan,
    int ActiveFieldCount,
    int SeasonCount,
    SeasonResponse? CurrentSeason);

public sealed record MemberCreateRequest(string? Email, MemberRole? Role);
public sealed record MemberUpdateRequest(MemberRole? Role);
public sealed record MemberResponse(
    Guid UserId,
    string DisplayName,
    string Email,
    MemberRole Role,
    DateTimeOffset JoinedAt);

public sealed record FieldWriteRequest(
    string? Name,
    decimal Acreage,
    string? DefaultCrop,
    string? SoilType);
public sealed record LastActivityResponse(ActivityKind Type, DateOnly ActivityDate);
public sealed record FieldResponse(
    Guid Id,
    string Name,
    decimal Acreage,
    string DefaultCrop,
    string? SoilType,
    FieldStatus Status,
    string? CurrentCrop,
    LastActivityResponse? LastActivity);
public sealed record FieldSeasonRollupResponse(
    Guid SeasonId,
    string SeasonName,
    int Year,
    string Crop,
    DateOnly? PlantedOn,
    decimal? YieldPerAcre,
    decimal InputCost,
    decimal HarvestValue,
    decimal NetValue,
    decimal? PriorYieldDeltaPercent);
public sealed record FieldDetailResponse(
    FieldResponse Field,
    IReadOnlyList<FieldSeasonRollupResponse> SeasonRollups);

public sealed record SeasonWriteRequest(int Year, string? Name, DateOnly StartsOn, DateOnly EndsOn);
public sealed record SeasonResponse(Guid Id, int Year, string Name, DateOnly StartsOn, DateOnly EndsOn);

public sealed record ActivityWriteRequest(
    Guid SeasonId,
    ActivityKind? Type,
    DateOnly ActivityDate,
    decimal? Quantity,
    string? QuantityUnit,
    decimal? CostAmount,
    decimal? RevenueAmount,
    string? Notes);
public sealed record ActivityCreatorResponse(Guid Id, string DisplayName);
public sealed record ActivityResponse(
    Guid Id,
    Guid OrganizationId,
    Guid FieldId,
    string FieldName,
    decimal FieldAcreage,
    Guid SeasonId,
    string SeasonName,
    ActivityKind Type,
    DateOnly ActivityDate,
    decimal? Quantity,
    string? QuantityUnit,
    decimal? CostAmount,
    decimal? RevenueAmount,
    string? Notes,
    ActivityCreatorResponse CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record DashboardMetrics(
    int ActiveFieldCount,
    decimal TotalAcreage,
    decimal? SeasonProgressPercent,
    int ActivitiesThisSeason,
    decimal InputCost,
    decimal HarvestValue,
    decimal NetValue,
    decimal? YieldPerAcre);
public sealed record CropProgressResponse(
    string Crop,
    decimal Acreage,
    int FieldCount,
    int? DaysToHarvest);
public sealed record DashboardLimits(int? MaxFields);
public sealed record DashboardResponse(
    OrganizationResponse Organization,
    SeasonResponse? CurrentSeason,
    DashboardMetrics Metrics,
    IReadOnlyList<ActivityResponse> RecentActivities,
    IReadOnlyList<CropProgressResponse> CropProgress,
    DashboardLimits Limits);

public sealed record InsightTotals(
    int ActiveFields,
    decimal TotalAcreage,
    decimal InputCost,
    decimal HarvestValue,
    decimal NetValue);
public sealed record YieldBySeasonResponse(int Year, string Crop, decimal YieldPerAcre);
public sealed record CostVsValueResponse(int Year, decimal InputCost, decimal HarvestValue);
public sealed record CropMixResponse(string Crop, decimal Acreage);
public sealed record FieldNetValueResponse(Guid FieldId, string FieldName, decimal NetValue);
public sealed record ActivityCountResponse(ActivityKind Type, int Count);
public sealed record InsightsResponse(
    Guid SelectedSeasonId,
    InsightTotals Totals,
    IReadOnlyList<YieldBySeasonResponse> YieldBySeason,
    IReadOnlyList<CostVsValueResponse> CostVsValue,
    IReadOnlyList<CropMixResponse> CropMix,
    IReadOnlyList<FieldNetValueResponse> FieldNetValue,
    IReadOnlyList<ActivityCountResponse> ActivityCountByType);

public sealed record ReportOrganization(Guid Id, string Name, PlanTier Plan);
public sealed record ReportSummary(
    int ActiveFields,
    decimal TotalAcreage,
    decimal InputCost,
    decimal HarvestValue,
    decimal NetValue,
    decimal? AverageYieldPerAcre);
public sealed record ReportField(
    Guid FieldId,
    string FieldName,
    string Crop,
    decimal Acreage,
    int ActivityCount,
    decimal InputCost,
    decimal HarvestValue,
    decimal? YieldPerAcre);
public sealed record SeasonReportResponse(
    ReportOrganization Organization,
    SeasonResponse Season,
    DateTimeOffset GeneratedAt,
    ReportSummary Summary,
    IReadOnlyList<ReportField> Fields,
    IReadOnlyList<ActivityCountResponse> ActivitySummary,
    IReadOnlyList<ActivityResponse> Activities);

public sealed record BillingLimits(
    int? MaxFields,
    bool CsvExportEnabled,
    bool SeasonReportEnabled);
public sealed record BillingUsage(int ActiveFieldCount);
public sealed record PlanChangeResponse(
    PlanTier FromPlan,
    PlanTier ToPlan,
    string ChangedBy,
    DateTimeOffset ChangedAt);
public sealed record BillingResponse(
    PlanTier Plan,
    BillingLimits Limits,
    BillingUsage Usage,
    IReadOnlyList<PlanChangeResponse> History);
public sealed record PlanMutationResponse(PlanTier Plan, DateTimeOffset ChangedAt);

public sealed record ErrorEnvelope(ApiError Error);
public sealed record ApiError(
    string Code,
    string Message,
    string TraceId,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null);
