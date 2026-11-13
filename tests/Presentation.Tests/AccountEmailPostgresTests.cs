using System.Text.Json;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Services;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.Domain.Email;
using LinerNotes.Presentation.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace LinerNotes.Presentation.Tests;

public sealed partial class CoordinatedAuthenticationPostgresTests
{
    private void EnableMail()
    {
        var options = services.GetRequiredService<EmailDeliveryOptions>(); options.Enabled = true;
        options.PublicOrigin = "https://mail.example.test"; options.FromAddress = "sender@example.test";
    }
    private async Task RequestMail(EmailPurpose purpose)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IEmailPayloadProtection>().Prepare();
        await scope.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(1_000_000, async ct =>
        { await scope.ServiceProvider.GetRequiredService<AccountEmailService>().RequestAsync("fixture@example.test", purpose, ct); return true; }, default);
    }
    private sealed class CapturingTransport : IEmailTransport
    {
        public List<EmailPayload> Payloads { get; } = [];
        public string Status = "accepted";
        public Task<EmailTransportResult> SendAsync(EmailPayload payload, string idempotencyKey, CancellationToken ct)
        { Payloads.Add(payload); return Task.FromResult(new EmailTransportResult(Status, Status == "accepted" ? Guid.NewGuid().ToString() : null)); }
        public Task<EmailTransportResult> ReconcileAsync(string id, CancellationToken ct) => Task.FromResult(new EmailTransportResult("delivered", id));
    }
    private async Task<EmailTickResult> Dispatch(CapturingTransport transport, TimeProvider? clock = null)
    {
        await using var scope = services.CreateAsyncScope(); var sp = scope.ServiceProvider;
        return await new EmailDispatcher(sp.GetRequiredService<IEmailDispatchStore>(), sp.GetRequiredService<IEmailPayloadProtection>(), transport,
            sp.GetRequiredService<IAccountEmailLease>(), sp.GetRequiredService<EmailDeliveryOptions>(), clock ?? TimeProvider.System).RunAsync(10, default);
    }
    [PostgresFact]
    public async Task ConfirmationRecoveryDispatchExportAndSuppressionUseTheRealCoordinatedLifecycle()
    {
        var id = await SeedAsync(); EnableMail(); await RequestMail(EmailPurpose.Confirmation); await RequestMail(EmailPurpose.Confirmation);
        await using (var pending = services.CreateAsyncScope())
        {
            var sp = pending.ServiceProvider; var rows = await sp.GetRequiredService<DataContext>().Set<EmailDispatch>().ToArrayAsync();
            Assert.Single(rows); Assert.DoesNotContain("Confirm your", rows[0].ProtectedPayload!);
            var exported = JsonSerializer.Serialize(await sp.GetRequiredService<IEmailDispatchStore>().ExportAsync(id, default));
            Assert.DoesNotContain("#id=", exported); Assert.Contains("capability redacted", exported);
        }
        var transport = new CapturingTransport(); Assert.Equal(1, (await Dispatch(transport)).Accepted);
        var confirmation = QueryHelpers.ParseQuery(new Uri(transport.Payloads.Single().SensitiveLinks.Single()).Fragment[1..]);
        await using (var consume = services.CreateAsyncScope())
        {
            var sp = consume.ServiceProvider;
            await sp.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(1_000_000, async ct =>
            {
                var account = sp.GetRequiredService<AccountEmailService>();
                Assert.False(await account.ConsumeAsync(id, confirmation["token"]!, "Wrong-purpose-fixture-password!"));
                Assert.True(await account.ConsumeAsync(id, confirmation["token"]!, null));
                Assert.True((await sp.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id.ToString()))!.EmailConfirmed);
                await sp.GetRequiredService<IAuthService>().StoreRefreshTokenAsync(id, "fixture-old-refresh"); return true;
            }, default);
        }
        await RequestMail(EmailPurpose.PasswordReset); Assert.Equal(1, (await Dispatch(transport)).Accepted);
        var reset = QueryHelpers.ParseQuery(new Uri(transport.Payloads.Last().SensitiveLinks.Single()).Fragment[1..]);
        await using (var consume = services.CreateAsyncScope())
        {
            var sp = consume.ServiceProvider;
            await sp.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(1_000_000, async ct =>
            {
                var account = sp.GetRequiredService<AccountEmailService>();
                Assert.True(await account.ConsumeAsync(id, reset["token"]!, "New-fixture-password-456!"));
                Assert.False(await account.ConsumeAsync(id, reset["token"]!, "Other-fixture-password-789!"));
                Assert.False(await sp.GetRequiredService<IAuthService>().ValidateRefreshTokenAsync(id, "fixture-old-refresh")); return true;
            }, default);
            var store = sp.GetRequiredService<IEmailDispatchStore>();
            var sent = await sp.GetRequiredService<DataContext>().Set<EmailDispatch>().OrderByDescending(x => x.CreatedAt).FirstAsync();
            Assert.Null(sent.ProtectedPayload); Assert.Null(sent.DeliveredAt);
            Assert.True(await store.EventAsync(new(sent.ProviderMessageId!, "delivered", DateTimeOffset.UtcNow), default));
            Assert.True(await store.EventAsync(new(sent.ProviderMessageId!, "complained", DateTimeOffset.UtcNow), default));
            Assert.True((await store.AccountAsync(id, default))!.Suppressed);
            Assert.Single(await store.SuppressionsAsync(id, default));
            await sp.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        }
    }
    private sealed class MailClock(DateTimeOffset now) : TimeProvider
    { public DateTimeOffset Now = now; public override DateTimeOffset GetUtcNow() => Now; }
    [PostgresFact]
    public async Task RestartRetriesFrozenPayloadAndClosedRetryWindowIsQuarantined()
    {
        var id = await SeedAsync(); EnableMail(); await RequestMail(EmailPurpose.Confirmation);
        var transport = new CapturingTransport { Status = "unknown" }; var clock = new MailClock(DateTimeOffset.UtcNow);
        Assert.Equal(1, (await Dispatch(transport, clock)).Deferred);
        clock.Now = clock.Now.AddMinutes(5); await Dispatch(transport, clock);
        Assert.Equal(2, transport.Payloads.Count); Assert.Equal(JsonSerializer.Serialize(transport.Payloads[0]), JsonSerializer.Serialize(transport.Payloads[1]));
        clock.Now = clock.Now.AddHours(23); Assert.Equal(1, (await Dispatch(transport, clock)).Stopped);
        await using var check = services.CreateAsyncScope();
        var row = await check.ServiceProvider.GetRequiredService<DataContext>().Set<EmailDispatch>().SingleAsync();
        Assert.Equal(EmailState.Ambiguous, row.State); Assert.Null(row.ProtectedPayload);
        await check.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }
    [PostgresFact]
    public async Task DispatchLeaseSerializesWorkersAndDeletionCascadesOwnedMail()
    {
        var id = await SeedAsync(); EnableMail(); await RequestMail(EmailPurpose.Confirmation);
        await using var scope = services.CreateAsyncScope(); var sp = scope.ServiceProvider;
        await using (var held = await sp.GetRequiredService<IEmailDispatchStore>().TryAcquireDispatchLeaseAsync(default))
        {
            Assert.NotNull(held); Assert.True((await Dispatch(new CapturingTransport())).Busy);
        }
        await using (var held = await sp.GetRequiredService<IAccountEmailLease>().AcquireAsync(id, default))
        {
            await sp.GetRequiredService<IDiscoveryWriteCoordinator>().ReclaimAsync(async ct =>
            { await sp.GetRequiredService<DataContext>().Users.Where(x => x.Id == id).ExecuteDeleteAsync(ct); return true; }, default);
        }
        Assert.Empty(await sp.GetRequiredService<DataContext>().Set<EmailDispatch>().ToArrayAsync());
        await sp.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }
    [PostgresFact]
    public async Task ProductionWeeklyQueueKeepsStoredPicksAndOneDispatchPerWeekAcrossRetries()
    {
        var id = await SeedAsync(); EnableMail();
        var clock = new MailClock(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
        var due = new DateTime(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc);
        await using (var seed = services.CreateAsyncScope())
        {
            var sp = seed.ServiceProvider;
            await sp.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(2_000_000, async ct =>
            {
                var manager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var identity = (await manager.FindByIdAsync(id.ToString()))!; identity.EmailConfirmed = true;
                Assert.True((await manager.UpdateAsync(identity)).Succeeded);
                var db = sp.GetRequiredService<DataContext>();
                await db.Users.Where(x => x.Id == id).ExecuteUpdateAsync(set => set.SetProperty(x => x.WeeklyEmailOptIn, true).SetProperty(x => x.NextDigestAt, due), ct);
                var track = LinerNotes.Domain.Catalog.Track.Create("Protocol fixture recording", "Protocol fixture artist");
                for (int index = 0; index < 2; index++)
                {
                    var slot = due.AddDays(index * 7); var week = LinerNotes.Domain.Digest.WeeklySchedulePolicy.Week(slot);
                    var digest = new LinerNotes.Domain.Digest.WeeklyDigest(id, week, slot.Date.AddDays(-6), slot.Date.AddDays(1));
                    // A fake Live response contract exercises delivery; this is not real-provider or listening evidence.
                    var response = new LinerNotes.Domain.Scoring.StoredResponse("ListenBrainz", "synthetic-protocol-fixture", "fixture", "Live", new('a', 64), clock.Now);
                    var score = new LinerNotes.Domain.Scoring.ScoreBreakdown
                    {
                        FinalScore = .5,
                        Snapshot = new()
                        {
                            ConfigurationVersion = "fixture",
                            ConfigurationSha256 = new('b', 64),
                            Weights = new(1, 0),
                            TrackKey = "fixture",
                            ArtistName = track.ArtistName,
                            Title = track.Title,
                            TasteWeights = new Dictionary<string, double>(),
                            CandidateWeights = new Dictionary<string, double>(),
                            Evidence = LinerNotes.Domain.Scoring.ScoreEvidence.Empty with { SupportingResponses = [response] }
                        }
                    };
                    digest.AddRecommendation(track, score, 1); db.WeeklyDigests.Add(digest);
                }
                await db.SaveChangesAsync(ct); return true;
            }, default);
        }
        var transport = new CapturingTransport();
        for (int iteration = 0; iteration < 3; iteration++)
        {
            await using var tick = services.CreateAsyncScope(); var sp = tick.ServiceProvider;
            var queue = new EmailQueue(sp.GetRequiredService<IEmailDispatchStore>(), sp.GetRequiredService<IEmailPayloadProtection>(), services.GetRequiredService<EmailDeliveryOptions>(), clock);
            var weekly = new WeeklyEmailQueue(sp.GetRequiredService<LinerNotes.Application.Common.Interfaces.Repositories.IWeeklyDigestRepository>(),
                sp.GetRequiredService<LinerNotes.Application.Common.Interfaces.Repositories.IUserRepository>(), sp.GetRequiredService<IDigestEmailRenderer>(), queue,
                services.GetRequiredService<EmailDeliveryOptions>());
            var sender = NSubstitute.Substitute.For<MediatR.ISender>();
            NSubstitute.SubstituteExtensions.Returns(sender.Send(NSubstitute.Arg.Any<LinerNotes.Application.Features.Digests.Commands.GenerateDigest.GenerateDigestCommand>(), NSubstitute.Arg.Any<CancellationToken>()),
                new LinerNotes.Application.Features.Digests.Commands.GenerateDigest.GenerateDigestResult("existing", null, []));
            var handler = new LinerNotes.Application.Features.Digests.Commands.RunScheduledTick.RunScheduledTickCommandHandler(sp.GetRequiredService<IWeeklyScheduleStore>(), sender, clock, weekly);
            var result = await handler.Handle(new([], 5, Production: true), default);
            Assert.True(result.Status == "completed", string.Join(",", result.Jobs.Select(j => j.Reason)));
            if (iteration < 2)
            {
                Assert.Single(result.Jobs); Assert.Equal("queued", result.Jobs[0].Status);
                Assert.True(await weekly.EnqueueAsync(id, LinerNotes.Domain.Digest.IsoWeek.Parse(result.Jobs[0].Week!), default));
                var dispatcher = new EmailDispatcher(sp.GetRequiredService<IEmailDispatchStore>(), sp.GetRequiredService<IEmailPayloadProtection>(), transport,
                    sp.GetRequiredService<IAccountEmailLease>(), services.GetRequiredService<EmailDeliveryOptions>(), clock);
                Assert.Equal(1, (await dispatcher.RunAsync(10, default)).Accepted);
            }
            else Assert.Empty(result.Jobs);
        }
        await using var verify = services.CreateAsyncScope(); var provider = verify.ServiceProvider;
        var rows = await provider.GetRequiredService<DataContext>().Set<EmailDispatch>().ToArrayAsync();
        Assert.Equal(2, rows.Length); Assert.All(rows, row => Assert.Equal(EmailState.Accepted, row.State));
        Assert.Equal(2, transport.Payloads.Count); Assert.All(transport.Payloads, payload =>
        { Assert.Contains("Protocol fixture recording", payload.Text); Assert.Contains("/account", payload.Text); Assert.Contains("List-Unsubscribe", payload.Headers.Keys); Assert.DoesNotContain("/manual-test", payload.Text); });
        await provider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }

    [PostgresFact]
    public async Task DailyBudgetAndExpiredPayloadCleanupSurviveNewScopes()
    {
        var id = await SeedAsync(); EnableMail(); services.GetRequiredService<EmailDeliveryOptions>().DailyLimit = 1;
        await RequestMail(EmailPurpose.Confirmation); await RequestMail(EmailPurpose.PasswordReset);
        var transport = new CapturingTransport(); var clock = new MailClock(DateTimeOffset.UtcNow);
        var first = await Dispatch(transport, clock); Assert.Equal(1, first.Accepted); Assert.Equal(1, first.Deferred);
        Assert.Equal(0, (await Dispatch(transport, clock)).Accepted);
        clock.Now = clock.Now.AddDays(2); await Dispatch(transport, clock);
        Assert.Single(transport.Payloads);
        await using var check = services.CreateAsyncScope();
        var rows = await check.ServiceProvider.GetRequiredService<DataContext>().Set<EmailDispatch>().ToArrayAsync();
        Assert.All(rows, row => Assert.Null(row.ProtectedPayload)); Assert.Contains(rows, row => row.State == EmailState.Cancelled);
        await check.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }
    [PostgresFact]
    public async Task ExpiredIdentityTokensAndStaleQueuedSecurityMessagesAreRejected()
    {
        var id = await SeedAsync(); EnableMail(); await RequestMail(EmailPurpose.PasswordReset);
        await using (var changed = services.CreateAsyncScope())
        {
            var sp = changed.ServiceProvider;
            await sp.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(1_000_000, async ct =>
            {
                var manager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var user = (await manager.FindByIdAsync(id.ToString()))!;
                var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RecoveryTokenProviderOptions>>();
                settings.Value.TokenLifespan = TimeSpan.FromSeconds(-1);
                var token = await manager.GeneratePasswordResetTokenAsync(user);
                Assert.False((await manager.ResetPasswordAsync(user, token, "Expired-token-fixture-password")).Succeeded);
                Assert.True((await manager.UpdateSecurityStampAsync(user)).Succeeded); return true;
            }, default);
        }
        var transport = new CapturingTransport(); Assert.Equal(1, (await Dispatch(transport)).Stopped); Assert.Empty(transport.Payloads);
    }

}
