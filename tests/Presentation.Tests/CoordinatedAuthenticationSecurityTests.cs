using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Services;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.IdentityEntities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed partial class CoordinatedAuthenticationPostgresTests
{
    [PostgresFact]
    public async Task ACorrectAttemptPreparedBeforeParallelFailuresCannotBypassTheResultingLockout()
    {
        var id = await SeedAsync();
        observation.Enabled = observation.Pause = true;
        var correct = LoginAsync(Password);
        await observation.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            observation.Pause = false;
            await using var state = services.CreateAsyncScope();
            int limit = state.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().Options.Lockout.MaxFailedAccessAttempts;
            var failures = await Task.WhenAll(Enumerable.Range(0, limit).Select(_ => LoginAsync("Wrong-parallel-fixture-123!")));
            Assert.All(failures, Assert.False);
            Assert.True(await state.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .IsLockedOutAsync((await state.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id.ToString()))!));
        }
        finally { observation.Release.Set(); }
        Assert.False(await correct);
        await using var check = services.CreateAsyncScope();
        Assert.Empty(await check.ServiceProvider.GetRequiredService<DataContext>().UserTokens.Where(token => token.UserId == id).ToArrayAsync());
    }

    [PostgresFact]
    public async Task ConcurrentStandardPasswordResetFencesAnAlreadyPreparedLogin()
    {
        var id = await SeedAsync();
        string reset;
        await using (var prepare = services.CreateAsyncScope())
        {
            var manager = prepare.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            reset = await manager.GeneratePasswordResetTokenAsync((await manager.FindByIdAsync(id.ToString()))!);
        }
        observation.Enabled = observation.Pause = true;
        var oldLogin = LoginAsync(Password);
        await observation.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            observation.Pause = false;
            await using var change = services.CreateAsyncScope();
            await change.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(1_000_000, async ct =>
            {
                var result = await change.ServiceProvider.GetRequiredService<IAuthService>()
                    .ResetPasswordAsync("fixture@example.test", reset, "Reset-fixture-only-456!");
                Assert.True(result.Succeeded, string.Join(",", result.Errors));
                return true;
            }, default);
        }
        finally { observation.Release.Set(); }
        Assert.False(await oldLogin);
        Assert.True(await LoginAsync("Reset-fixture-only-456!"));
    }

    [PostgresFact]
    public async Task StandardSecurityStampChangeInvalidatesAPersistedRefreshSession()
    {
        var id = await SeedAsync();
        const string token = "fixture-only-old-session";
        await using (var seed = services.CreateAsyncScope())
            await seed.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(1_000_000, async ct =>
            {
                await seed.ServiceProvider.GetRequiredService<IAuthService>().StoreRefreshTokenAsync(id, token);
                return true;
            }, default);
        await using (var before = services.CreateAsyncScope())
            Assert.True(await before.ServiceProvider.GetRequiredService<IAuthService>().ValidateRefreshTokenAsync(id, token));
        await using (var change = services.CreateAsyncScope())
            await change.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(1_000_000, async ct =>
            {
                var manager = change.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                Assert.True((await manager.UpdateSecurityStampAsync((await manager.FindByIdAsync(id.ToString()))!)).Succeeded);
                return true;
            }, default);
        await using var check = services.CreateAsyncScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<IAuthService>().ValidateRefreshTokenAsync(id, token));
        await check.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }
}
