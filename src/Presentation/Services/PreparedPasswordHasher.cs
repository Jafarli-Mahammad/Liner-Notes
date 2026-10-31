using LinerNotes.DataAccess.IdentityEntities;
using Microsoft.AspNetCore.Identity;

namespace LinerNotes.Presentation.Services;

/// <summary>Request-local precomputation using Identity's hasher. UserManager still owns every authentication mutation.</summary>
public sealed class PreparedPasswordHasher(PasswordHasher<ApplicationUser> inner) : IPasswordHasher<ApplicationUser>
{
    private Attempt? attempt;
    public bool HasAttempt => attempt is not null;

    public IDisposable Prepare(ApplicationUser? user, string password)
    {
        if (attempt is not null) throw new InvalidOperationException("authentication_attempt_already_prepared");
        var result = user?.PasswordHash is { } hash && !user.IsDeleted
            ? inner.VerifyHashedPassword(user, hash, password) : PasswordVerificationResult.Failed;
        string? replacement = result == PasswordVerificationResult.SuccessRehashNeeded ? inner.HashPassword(user!, password) : null;
        attempt = new(user, password, result, replacement);
        return new Scope(this);
    }

    public bool Matches(ApplicationUser current) => attempt?.Matches(current) == true;

    public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        if (attempt is null) return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        if (!attempt.Matches(user) || !ReferenceEquals(providedPassword, attempt.Password) || hashedPassword != attempt.PasswordHash)
            throw new InvalidOperationException("authentication_snapshot_changed");
        return attempt.Result;
    }

    public string HashPassword(ApplicationUser user, string password)
    {
        if (attempt is null) return inner.HashPassword(user, password);
        if (user.Id != attempt.UserId || !ReferenceEquals(password, attempt.Password) || attempt.Replacement is null)
            throw new InvalidOperationException("authentication_rehash_not_prepared");
        return attempt.Replacement;
    }

    private sealed class Attempt
    {
        public Guid UserId { get; }
        public string? PasswordHash { get; }
        public string Password { get; }
        public PasswordVerificationResult Result { get; }
        public string? Replacement { get; }
        private readonly string? stamp, name, email;
        private readonly bool active, emailConfirmed, phoneConfirmed, twoFactor, lockoutEnabled;
        public Attempt(ApplicationUser? user, string password, PasswordVerificationResult result, string? replacement)
        {
            UserId = user?.Id ?? Guid.Empty; PasswordHash = user?.PasswordHash; stamp = user?.SecurityStamp;
            active = user is { IsDeleted: false };
            name = user?.NormalizedUserName; email = user?.NormalizedEmail;
            emailConfirmed = user?.EmailConfirmed == true; phoneConfirmed = user?.PhoneNumberConfirmed == true;
            twoFactor = user?.TwoFactorEnabled == true; lockoutEnabled = user?.LockoutEnabled == true;
            Password = password; Result = result; Replacement = replacement;
        }
        // Failure counts/concurrency stamps may advance between attempts. Apply the result only to
        // reloaded current state under coordination, so each failed guess is counted and lockout wins.
        public bool Matches(ApplicationUser user) => active && user.Id == UserId && !user.IsDeleted &&
            user.PasswordHash == PasswordHash && user.SecurityStamp == stamp && user.NormalizedUserName == name &&
            user.NormalizedEmail == email && user.EmailConfirmed == emailConfirmed && user.PhoneNumberConfirmed == phoneConfirmed &&
            user.TwoFactorEnabled == twoFactor && user.LockoutEnabled == lockoutEnabled;
    }

    private sealed class Scope(PreparedPasswordHasher owner) : IDisposable
    { public void Dispose() => owner.attempt = null; }
}
