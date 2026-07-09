using FieldLedger.Api;

namespace FieldLedger.Api.Tests;

public sealed class EntitlementGateTests
{
    [Fact]
    public void FreeFieldLimit_BlocksFourthActiveField()
    {
        Assert.True(EntitlementGate.CanCreateActiveField(3, 2).Allowed);
        var denied = EntitlementGate.CanCreateActiveField(3, 3);
        Assert.False(denied.Allowed);
        Assert.Equal("field_limit_reached", denied.Code);
        Assert.True(EntitlementGate.CanCreateActiveField(null, 50).Allowed);
    }

    [Theory]
    [InlineData(MemberRole.Owner, true)]
    [InlineData(MemberRole.Agronomist, true)]
    [InlineData(MemberRole.Viewer, false)]
    public void CsvExport_RequiresProAndEditingRole(MemberRole role, bool allowed)
    {
        Assert.Equal(allowed, EntitlementGate.CanExportCsv(true, role).Allowed);
        Assert.Equal("pro_required", EntitlementGate.CanExportCsv(false, role).Code);
    }

    [Fact]
    public void SeasonReport_RequiresEnabledEntitlement()
    {
        Assert.True(EntitlementGate.CanViewSeasonReport(true).Allowed);
        Assert.Equal("pro_required", EntitlementGate.CanViewSeasonReport(false).Code);
    }
}
