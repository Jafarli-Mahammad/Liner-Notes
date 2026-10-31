using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.DataAccess.IdentityEntities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed partial class CoordinatedAuthenticationPostgresTests
{
    [PostgresFact]
    public async Task PasswordChangedDuringPreverificationRejectsTheOldCredential()
    {
        var id = await SeedAsync();
        observation.Enabled = observation.Pause = true;
        var login = LoginAsync(Password);
        await observation.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            // The original verifier remains paused; let the password-change verifier proceed.
            observation.Pause = false;
            await using var change = services.CreateAsyncScope();
            await change.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(1_000_000, async ct =>
            {
                var manager = change.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var current = await manager.FindByIdAsync(id.ToString());
                Assert.True((await manager.ChangePasswordAsync(current!, Password, "Replacement-fixture-only-456!")).Succeeded);
                return true;
            }, default);
        }
        finally { observation.Release.Set(); }
        Assert.False(await login);
        Assert.True(await LoginAsync("Replacement-fixture-only-456!"));
    }

    [PostgresFact]
    public async Task SecurityStampChangeDuringPreverificationRejectsTheSnapshot()
    {
        var id = await SeedAsync();
        observation.Enabled = observation.Pause = true;
        var login = LoginAsync(Password);
        await observation.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await using var change = services.CreateAsyncScope();
            await change.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(1_000_000, async ct =>
            {
                var manager = change.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                Assert.True((await manager.UpdateSecurityStampAsync((await manager.FindByIdAsync(id.ToString()))!)).Succeeded);
                return true;
            }, default);
        }
        finally { observation.Pause = false; observation.Release.Set(); }
        Assert.False(await login);
        Assert.True(await LoginAsync(Password));
    }

    [PostgresFact]
    public async Task LoginUsesTheStandardIdentityRehashOutsideTheStorageTransaction()
    {
        var id = await SeedAsync();
        string oldHash;
        await using (var change = services.CreateAsyncScope())
        {
            oldHash = await change.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(1_000_000, async ct =>
            {
                var manager = change.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = (await manager.FindByIdAsync(id.ToString()))!;
                user.PasswordHash = new PasswordHasher<ApplicationUser>(Microsoft.Extensions.Options.Options.Create(
                    new PasswordHasherOptions { IterationCount = 10_000 })).HashPassword(user, Password);
                Assert.True((await manager.UpdateAsync(user)).Succeeded);
                return user.PasswordHash;
            }, default);
        }
        observation.Enabled = true;
        Assert.True(await LoginAsync(Password));
        Assert.Equal(0, observation.HashesInsideTransaction);
        await using var check = services.CreateAsyncScope();
        var current = (await check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id.ToString()))!;
        Assert.NotEqual(oldHash, current.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<ApplicationUser>().VerifyHashedPassword(current, current.PasswordHash!, Password));
        await check.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }
}
