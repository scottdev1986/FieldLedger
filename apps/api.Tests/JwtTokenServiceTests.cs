using System.IdentityModel.Tokens.Jwt;
using FieldLedger.Api;
using Microsoft.IdentityModel.Tokens;

namespace FieldLedger.Api.Tests;

public sealed class JwtTokenServiceTests
{
    private static readonly AuthOptions Options = new(
        "test-secret-that-is-at-least-thirty-two-bytes-long",
        "fieldledger-api",
        "fieldledger",
        60);

    [Fact]
    public void IssueAndValidate_RoundTripsRequiredClaims()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero));
        var service = new JwtTokenService(Options, clock);
        var user = new AppUser(
            Guid.NewGuid(), "farmer@example.com", "Alex Farmer", clock.GetUtcNow());

        var token = service.Issue(user);
        var principal = service.Validate(token);

        Assert.Equal(user.Id.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(user.Email, principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value);
        Assert.Equal(user.DisplayName, principal.FindFirst(JwtRegisteredClaimNames.Name)?.Value);
    }

    [Fact]
    public void Validate_RejectsExpiredToken()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero));
        var service = new JwtTokenService(Options, clock);
        var token = service.Issue(new AppUser(
            Guid.NewGuid(), "farmer@example.com", "Alex Farmer", clock.GetUtcNow()));

        clock.Advance(TimeSpan.FromMinutes(61));

        Assert.Throws<SecurityTokenExpiredException>(() => service.Validate(token));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan amount) => now = now.Add(amount);
    }
}
