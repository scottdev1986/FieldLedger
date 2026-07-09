using FieldLedger.Api;
using Microsoft.AspNetCore.Identity;

namespace FieldLedger.Api.Tests;

public sealed class PasswordHashTests
{
    [Fact]
    public void PasswordHasher_HashesAndVerifiesWithoutStoringPlaintext()
    {
        var hasher = new PasswordHasher<AppUser>();
        var user = new AppUser(Guid.NewGuid(), "owner@example.com", "Owner", DateTimeOffset.UtcNow);

        var hash = hasher.HashPassword(user, "correct-horse-battery-staple");

        Assert.DoesNotContain("correct-horse-battery-staple", hash);
        Assert.NotEqual(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(user, hash, "correct-horse-battery-staple"));
        Assert.Equal(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(user, hash, "wrong-password"));
    }
}
