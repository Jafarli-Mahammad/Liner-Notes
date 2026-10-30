using System.Threading.RateLimiting;
using LinerNotes.Application.Common.Diagnostics;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess.IdentityEntities;
using Microsoft.AspNetCore.Identity;

namespace LinerNotes.Presentation.Services;

public sealed class PasswordVerificationGate : IDisposable
{
    private readonly ConcurrencyLimiter limiter = new(new()
    { PermitLimit = 2, QueueLimit = 32, QueueProcessingOrder = QueueProcessingOrder.OldestFirst });
    public async Task<RateLimitLease> AcquireAsync(CancellationToken ct)
    {
        var lease = await limiter.AcquireAsync(1, ct);
        if (lease.IsAcquired) return lease;
        lease.Dispose();
        throw new GenerationStoppedException("authentication_busy");
    }
    public void Dispose() => limiter.Dispose();
}

public sealed class PasswordVerificationPreparation(UserManager<ApplicationUser> users,
    PreparedPasswordHasher hasher, PasswordVerificationGate gate)
{
    public async Task<IDisposable> PrepareAsync(string email, string password, CancellationToken ct)
    {
        using var measurement = DiscoveryDiagnostics.Measure("authentication.preverification");
        using var lease = await gate.AcquireAsync(ct);
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(email);
        ct.ThrowIfCancellationRequested();
        return hasher.Prepare(user, password);
    }
}
