using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.Presentation.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class PreparedPasswordHasherTests
{
    private const string Password = "Disposable-test-only-123!";

    [Fact]
    public void PreparedVerificationAndRehashUseIdentityExactlyOnceOutsideTheManagedWrite()
    {
        var user = User();
        user.PasswordHash = new PasswordHasher<ApplicationUser>(Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions { IterationCount = 10_000 }))
            .HashPassword(user, Password);
        var inner = new CountedHasher(); var prepared = new PreparedPasswordHasher(inner);
        using (prepared.Prepare(user, Password))
        {
            Assert.Equal(1, inner.Verifications); Assert.Equal(1, inner.Hashes);
            Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, prepared.VerifyHashedPassword(user, user.PasswordHash, Password));
            string replacement = prepared.HashPassword(user, Password);
            Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user, replacement, Password));
            Assert.Equal(1, inner.Verifications); Assert.Equal(1, inner.Hashes);
        }
        Assert.False(prepared.HasAttempt);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("stamp")]
    [InlineData("identity")]
    [InlineData("deleted")]
    [InlineData("two-factor")]
    public void SecurityStateChangesCannotUseAPreparedResult(string mutation)
    {
        var user = User(); var inner = new CountedHasher(); user.PasswordHash = inner.HashPassword(user, Password);
        var prepared = new PreparedPasswordHasher(inner);
        using var attempt = prepared.Prepare(user, Password);
        switch (mutation)
        {
            case "password": user.PasswordHash = inner.HashPassword(user, "Different-test-only-123!"); break;
            case "stamp": user.SecurityStamp = Guid.NewGuid().ToString(); break;
            case "identity": user.NormalizedEmail = "CHANGED@EXAMPLE.TEST"; break;
            case "deleted": user.IsDeleted = true; break;
            case "two-factor": user.TwoFactorEnabled = true; break;
        }
        Assert.False(prepared.Matches(user));
        Assert.Throws<InvalidOperationException>(() => prepared.VerifyHashedPassword(user, user.PasswordHash, Password));
    }

    [Fact]
    public void FailedCountsAreAppliedToFreshStateWithoutDiscardingOtherwiseValidVerification()
    {
        var user = User(); var inner = new CountedHasher(); user.PasswordHash = inner.HashPassword(user, Password);
        var prepared = new PreparedPasswordHasher(inner);
        using var attempt = prepared.Prepare(user, Password);
        user.AccessFailedCount = 4; user.ConcurrencyStamp = Guid.NewGuid().ToString();
        Assert.True(prepared.Matches(user));
        Assert.Equal(PasswordVerificationResult.Success, prepared.VerifyHashedPassword(user, user.PasswordHash, Password));
        // The authentication service must still check current lockout and use UserManager's reset/update.
    }

    [Fact]
    public void MissingIdentityCannotBecomeAnAuthenticationAttemptForANewlyCreatedAccount()
    {
        var user = User(); var prepared = new PreparedPasswordHasher(new PasswordHasher<ApplicationUser>());
        using var attempt = prepared.Prepare(null, Password);
        Assert.False(prepared.Matches(user));
    }

    private static ApplicationUser User() => new()
    { Id = Guid.NewGuid(), NormalizedUserName = "FIXTURE", NormalizedEmail = "FIXTURE@EXAMPLE.TEST", SecurityStamp = Guid.NewGuid().ToString(), LockoutEnabled = true };

    private sealed class CountedHasher : PasswordHasher<ApplicationUser>
    {
        public int Verifications, Hashes;
        public override string HashPassword(ApplicationUser user, string password)
        { Hashes++; return base.HashPassword(user, password); }
        public override PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hash, string password)
        { Verifications++; return base.VerifyHashedPassword(user, hash, password); }
    }
}
