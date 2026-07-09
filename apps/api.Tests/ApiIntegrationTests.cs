using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FieldLedger.Api;
using Microsoft.IdentityModel.Tokens;

namespace FieldLedger.Api.Tests;

public sealed class ApiIntegrationTests
{
    private const string Password = "integration-password-2026";

    [DatabaseFact]
    public async Task Auth_RegisterDuplicateLoginWrongPasswordAndMe()
    {
        await using var host = await ApiTestHost.StartAsync();
        var email = UniqueEmail("auth");
        var registration = await RegisterAsync(host.Client, email, "Integration Owner");
        Assert.Equal(HttpStatusCode.Created, registration.Response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(registration.Token));

        var duplicate = await host.Client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            displayName = "Duplicate",
            password = Password
        });
        await AssertErrorAsync(duplicate, HttpStatusCode.Conflict, "email_already_registered");

        var login = await host.Client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginToken = (await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(loginToken));

        var wrong = await host.Client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });
        await AssertErrorAsync(wrong, HttpStatusCode.Unauthorized, "invalid_credentials");
        var unknown = await host.Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = UniqueEmail("unknown"),
            password = Password
        });
        await AssertErrorAsync(unknown, HttpStatusCode.Unauthorized, "invalid_credentials");

        UseToken(host.Client, loginToken!);
        var me = await host.Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meJson = await JsonDocument.ParseAsync(await me.Content.ReadAsStreamAsync());
        Assert.Equal(email, meJson.RootElement.GetProperty("user").GetProperty("email").GetString());
    }

    [DatabaseFact]
    public async Task Authentication_MissingInvalidTamperedAndExpiredTokensReturn401()
    {
        await using var host = await ApiTestHost.StartAsync();
        await AssertErrorAsync(await host.Client.GetAsync("/api/orgs"), HttpStatusCode.Unauthorized, "unauthorized");

        UseToken(host.Client, "not-a-jwt");
        await AssertErrorAsync(await host.Client.GetAsync("/api/orgs"), HttpStatusCode.Unauthorized, "unauthorized");

        var registration = await RegisterAsync(host.Client, UniqueEmail("token"), "Token User");
        UseToken(host.Client, registration.Token![..^1] + (registration.Token[^1] == 'a' ? 'b' : 'a'));
        await AssertErrorAsync(await host.Client.GetAsync("/api/orgs"), HttpStatusCode.Unauthorized, "unauthorized");

        UseToken(host.Client, CreateExpiredToken(registration.UserId));
        await AssertErrorAsync(await host.Client.GetAsync("/api/orgs"), HttpStatusCode.Unauthorized, "unauthorized");
    }

    [DatabaseFact]
    public async Task TenantIsolation_UnknownOrganizationIsNotVisible()
    {
        await using var host = await ApiTestHost.StartAsync();
        var registration = await RegisterAsync(host.Client, UniqueEmail("tenant"), "Tenant User");
        UseToken(host.Client, registration.Token!);

        var response = await host.Client.GetAsync($"/api/orgs/{Guid.NewGuid()}/fields");
        await AssertErrorAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [DatabaseFact]
    public async Task ActivityRoles_ViewerDeniedAndAgronomistAllowed()
    {
        await using var host = await ApiTestHost.StartAsync();
        var owner = await RegisterAsync(host.Client, UniqueEmail("owner"), "Owner");
        var agronomist = await RegisterAsync(host.Client, UniqueEmail("agronomist"), "Agronomist");
        var viewer = await RegisterAsync(host.Client, UniqueEmail("viewer"), "Viewer");

        UseToken(host.Client, owner.Token!);
        var orgId = await CreateOrganizationAsync(host.Client, "role-org");
        await AddMemberAsync(host.Client, orgId, agronomist.Email, "agronomist");
        await AddMemberAsync(host.Client, orgId, viewer.Email, "viewer");
        var seasonId = await CreateSeasonAsync(host.Client, orgId);
        var fieldId = await CreateFieldAsync(host.Client, orgId, "North Field");
        var activityBody = new
        {
            seasonId,
            type = "planting",
            activityDate = "2026-04-20",
            quantity = 10m,
            quantityUnit = "bags",
            costAmount = 500m,
            revenueAmount = (decimal?)null,
            notes = "Integration test"
        };

        UseToken(host.Client, viewer.Token!);
        var denied = await host.Client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/fields/{fieldId}/activities", activityBody);
        await AssertErrorAsync(denied, HttpStatusCode.Forbidden, "forbidden");

        UseToken(host.Client, agronomist.Token!);
        var allowed = await host.Client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/fields/{fieldId}/activities", activityBody);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }

    [DatabaseFact]
    public async Task Entitlements_FreeFieldLimitAndExportGateThenProSucceeds()
    {
        await using var host = await ApiTestHost.StartAsync();
        var owner = await RegisterAsync(host.Client, UniqueEmail("entitlement"), "Entitlement Owner");
        UseToken(host.Client, owner.Token!);
        var orgId = await CreateOrganizationAsync(host.Client, "entitlement-org");
        var seasonId = await CreateSeasonAsync(host.Client, orgId);

        await CreateFieldAsync(host.Client, orgId, "Field One");
        await CreateFieldAsync(host.Client, orgId, "Field Two");
        await CreateFieldAsync(host.Client, orgId, "Field Three");
        var fourth = await host.Client.PostAsJsonAsync($"/api/orgs/{orgId}/fields", FieldBody("Field Four"));
        await AssertErrorAsync(fourth, HttpStatusCode.UnprocessableEntity, "field_limit_reached");

        var freeExport = await host.Client.GetAsync($"/api/orgs/{orgId}/exports/activities.csv?seasonId={seasonId}");
        await AssertErrorAsync(freeExport, HttpStatusCode.Forbidden, "pro_required");
        var freeReport = await host.Client.GetAsync($"/api/orgs/{orgId}/seasons/{seasonId}/report");
        await AssertErrorAsync(freeReport, HttpStatusCode.Forbidden, "pro_required");

        var upgrade = await host.Client.PostAsync($"/api/orgs/{orgId}/billing/upgrade", null);
        Assert.Equal(HttpStatusCode.OK, upgrade.StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await host.Client.PostAsJsonAsync($"/api/orgs/{orgId}/fields", FieldBody("Field Four"))).StatusCode);
        var proExport = await host.Client.GetAsync($"/api/orgs/{orgId}/exports/activities.csv?seasonId={seasonId}");
        Assert.Equal(HttpStatusCode.OK, proExport.StatusCode);
        Assert.StartsWith("text/csv", proExport.Content.Headers.ContentType?.ToString());
        Assert.Equal(HttpStatusCode.OK,
            (await host.Client.GetAsync($"/api/orgs/{orgId}/seasons/{seasonId}/report")).StatusCode);
    }

    [DatabaseFact]
    public async Task Billing_OwnerOnlyAuditAndDowngradeFieldRule()
    {
        await using var host = await ApiTestHost.StartAsync();
        var owner = await RegisterAsync(host.Client, UniqueEmail("bill-owner"), "Billing Owner");
        var agronomist = await RegisterAsync(host.Client, UniqueEmail("bill-ag"), "Billing Agronomist");
        var viewer = await RegisterAsync(host.Client, UniqueEmail("bill-viewer"), "Billing Viewer");
        UseToken(host.Client, owner.Token!);
        var orgId = await CreateOrganizationAsync(host.Client, "billing-org");
        await AddMemberAsync(host.Client, orgId, agronomist.Email, "agronomist");
        await AddMemberAsync(host.Client, orgId, viewer.Email, "viewer");

        UseToken(host.Client, agronomist.Token!);
        await AssertErrorAsync(
            await host.Client.PostAsync($"/api/orgs/{orgId}/billing/upgrade", null),
            HttpStatusCode.Forbidden,
            "forbidden");
        UseToken(host.Client, viewer.Token!);
        await AssertErrorAsync(
            await host.Client.PostAsync($"/api/orgs/{orgId}/billing/upgrade", null),
            HttpStatusCode.Forbidden,
            "forbidden");

        UseToken(host.Client, owner.Token!);
        Assert.Equal(HttpStatusCode.OK,
            (await host.Client.PostAsync($"/api/orgs/{orgId}/billing/upgrade", null)).StatusCode);
        for (var number = 1; number <= 4; number++)
            await CreateFieldAsync(host.Client, orgId, $"Downgrade Field {number}");
        await AssertErrorAsync(
            await host.Client.PostAsync($"/api/orgs/{orgId}/billing/downgrade", null),
            HttpStatusCode.UnprocessableEntity,
            "too_many_active_fields_for_free");

        var billing = await host.Client.GetAsync($"/api/orgs/{orgId}/billing");
        Assert.Equal(HttpStatusCode.OK, billing.StatusCode);
        var json = await JsonDocument.ParseAsync(await billing.Content.ReadAsStreamAsync());
        Assert.Equal("pro", json.RootElement.GetProperty("plan").GetString());
        Assert.Contains(json.RootElement.GetProperty("history").EnumerateArray(),
            item => item.GetProperty("toPlan").GetString() == "pro");
    }

    private static async Task<(HttpResponseMessage Response, string? Token, Guid UserId, string Email)> RegisterAsync(
        HttpClient client,
        string email,
        string displayName)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            displayName,
            password = Password
        });
        if (!response.IsSuccessStatusCode)
            return (response, null, Guid.Empty, email);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return (
            response,
            json.RootElement.GetProperty("accessToken").GetString(),
            json.RootElement.GetProperty("user").GetProperty("id").GetGuid(),
            email);
    }

    private static async Task<Guid> CreateOrganizationAsync(HttpClient client, string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var response = await client.PostAsJsonAsync("/api/orgs", new
        {
            name = $"{prefix} {suffix}",
            slug = $"{prefix}-{suffix}"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("organization").GetProperty("id").GetGuid();
    }

    private static async Task AddMemberAsync(HttpClient client, Guid orgId, string email, string role)
    {
        var response = await client.PostAsJsonAsync($"/api/orgs/{orgId}/members", new { email, role });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<Guid> CreateSeasonAsync(HttpClient client, Guid orgId)
    {
        var response = await client.PostAsJsonAsync($"/api/orgs/{orgId}/seasons", new
        {
            year = 2026,
            name = "2026 Growing Season",
            startsOn = "2026-03-01",
            endsOn = "2026-11-30"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("season").GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateFieldAsync(HttpClient client, Guid orgId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/orgs/{orgId}/fields", FieldBody(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("field").GetProperty("id").GetGuid();
    }

    private static object FieldBody(string name) => new
    {
        name,
        acreage = 25.5m,
        defaultCrop = "corn",
        soilType = (string?)null
    };

    private static void UseToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var error = json.RootElement.GetProperty("error");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("traceId").GetString()));
    }

    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@fieldledger.test";

    private static string CreateExpiredToken(Guid userId)
    {
        const string secret = "fieldledger-integration-test-secret-0123456789";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var token = new JwtSecurityToken(
            issuer: "fieldledger-api",
            audience: "fieldledger",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
