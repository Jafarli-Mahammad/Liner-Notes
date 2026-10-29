// Measurement only. No Identity user store or production authentication boundary is changed.
using System.Diagnostics;
using LinerNotes.DataAccess.IdentityEntities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

internal static partial class Audit
{
    static void AuthBoundary()
    {
        var user = new ApplicationUser();
        // Generated fixture only; never accept a real password/hash as diagnostic input.
        const string password = "Disposable-probe-only-7!";
        var current = new PasswordHasher<ApplicationUser>();
        var old = new PasswordHasher<ApplicationUser>(Options.Create(new PasswordHasherOptions { IterationCount = 10_000 }));
        var currentHash = current.HashPassword(user, password); var oldHash = old.HashPassword(user, password);
        foreach (var scenario in new[] { "success", "failure", "rehash-required" })
        {
            var samples = new List<object>();
            for (int index = -1; index < 25; index++)
            {
                long allocation = GC.GetTotalAllocatedBytes(true); var timer = Stopwatch.StartNew();
                var result = current.VerifyHashedPassword(user, scenario == "rehash-required" ? oldHash : currentHash,
                    scenario == "failure" ? "wrong-disposable-password" : password);
                // CheckPasswordAsync also computes a replacement hash before writing it through Identity.
                if (result == PasswordVerificationResult.SuccessRehashNeeded) _ = current.HashPassword(user, password);
                timer.Stop();
                if (index >= 0) samples.Add(new { ms = timer.Elapsed.TotalMilliseconds,
                    allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocation, result = result.ToString() });
            }
            Emit(new { scenario = "identity-hash-" + scenario, defaultIterations = new PasswordHasherOptions().IterationCount, samples,
                limits = "Hash primitive only. Database writes, lockout, security stamps and admission are not replaced or simulated." });
        }
    }
}
