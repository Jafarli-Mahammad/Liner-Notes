using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using LinerNotes.Presentation.Background;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class DiscoveryJobServiceTests
{
    [Fact]
    public async Task PreparedProfileCompletesWhileAcquisitionForAnotherProfileIsBlocked()
    {
        var slow = ListenerProfile.Guest(DateTimeOffset.UtcNow, TimeSpan.FromDays(7));
        var ready = ListenerProfile.Guest(DateTimeOffset.UtcNow, TimeSpan.FromDays(7));
        var acquiring = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new Dictionary<Guid, TaskCompletionSource> {
            [slow.Id] = new(TaskCreationOptions.RunContinuationsAsynchronously), [ready.Id] = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var queue = new List<DiscoveryJob>();
        var gate = new object();
        DiscoveryJob Create(ListenerProfile profile) => new(profile.Id, "initial", new string('a', 64), DiscoveryJobKind.Generation,
            0, "{\"Kind\":0}", DateTimeOffset.UtcNow);
        queue.Add(Create(slow));
        var jobs = Substitute.For<IDiscoveryJobStore>();
        DiscoveryJob? Find(DiscoveryJobLane lane, DateTimeOffset now)
            => queue.FirstOrDefault(j => (j.State is DiscoveryJobState.Pending or DiscoveryJobState.Retryable) && j.NextAttemptAtUtc <= now &&
                (lane == DiscoveryJobLane.Acquisition ? j.ProgressCode == "waiting_for_music" : j.ProgressCode != "waiting_for_music"));
        jobs.HasReadyAsync(DiscoveryJobKind.Generation, Arg.Any<DiscoveryJobLane>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(c => { lock (gate) return Task.FromResult(Find(c.ArgAt<DiscoveryJobLane>(1), c.ArgAt<DateTimeOffset>(2)) is not null); });
        jobs.LeaseNextAsync(DiscoveryJobKind.Generation, Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<DiscoveryJobLane?>())
            .Returns(c => {
                lock (gate) {
                    var job = Find(c.ArgAt<DiscoveryJobLane?>(5)!.Value, c.ArgAt<DateTimeOffset>(1));
                    job?.AcquireLease(Guid.NewGuid(), c.ArgAt<DateTimeOffset>(1), c.ArgAt<TimeSpan>(2), c.ArgAt<int>(3));
                    return Task.FromResult(job);
                }
            });
        jobs.DeferAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(c => { lock (gate) queue.Single(j => j.Id == c.ArgAt<Guid>(0)).DeferLease(c.ArgAt<Guid>(1), c.ArgAt<DateTimeOffset>(2), c.ArgAt<TimeSpan>(3), c.ArgAt<string>(4)); return Task.CompletedTask; });
        var batches = Substitute.For<IDiscoveryBatchStore>();
        batches.ReadInputsAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(c => {
            var profile = c.ArgAt<Guid>(0) == slow.Id ? slow : ready;
            var signal = TasteSignal.ForProfile(profile.Id, null, TasteTargetType.Tag, profile.Id == slow.Id ? "ambient" : "jazz",
                1, TasteSignalSource.InitialSeedManual, "Selected style.");
            return Task.FromResult<DiscoveryInputs?>(new(profile, [signal], []));
        });
        batches.FindExcludedAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlySet<string>>(new HashSet<string>()));
        batches.CompleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DiscoveryBatchKind>(), Arg.Any<IReadOnlyList<BaselineAPick>>(),
            Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(c => {
                lock (gate) {
                    var job = queue.Single(j => j.Id == c.ArgAt<Guid>(0));
                    Assert.NotEmpty(c.ArgAt<IReadOnlyList<BaselineAPick>>(4));
                    job.Complete(c.ArgAt<Guid>(1), DateTimeOffset.UtcNow, c.ArgAt<Guid>(2), null);
                    completed[job.ProfileId].TrySetResult();
                }
                return Task.CompletedTask;
            });
        var admission = new Admission();
        var preparation = Substitute.For<IDiscoveryPreparation>();
        preparation.GetAsync(Arg.Any<DiscoveryPoolRequest>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(async c => {
            Assert.False(admission.Active);
            if (c.ArgAt<DiscoveryPoolRequest>(0).Seed == "ambient") {
                if (!c.ArgAt<bool>(1)) throw new DiscoveryPreparationRequiredException();
                acquiring.TrySetResult(); await release.Task.WaitAsync(c.ArgAt<CancellationToken>(2));
            }
            return Pool();
        });
        var resolver = Substitute.For<IRecommendationProviderResolver>();
        resolver.Resolve().Returns(new RecommendationProvider("listenbrainz", Substitute.For<IRecommendationSource>(),
            Substitute.For<ICandidateHydrator>(), Substitute.For<ISeedTagSource>()));
        using var services = new ServiceCollection().AddSingleton(jobs).AddSingleton(batches).AddSingleton(preparation).AddSingleton(resolver)
            .AddSingleton<IDiscoveryWriteCoordinator>(admission).AddSingleton(new GenerationConfiguration { Origin = EvidenceOrigin.Synthetic })
            .AddSingleton(new DiscoveryRuntimeOptions { RunJobs = true, LeaseSeconds = 15, MaximumWorkSeconds = 30 })
            .AddSingleton(new DiscoveryTasteOptions()).AddSingleton<TimeProvider>(TimeProvider.System).AddSingleton<BaselineAScorer>()
            .AddScoped<MaterializeDiscoveryTaste>().AddScoped<PrepareDiscoveryBatch>().AddScoped<RunDiscoveryJob>().BuildServiceProvider();
        using var runner = new DiscoveryJobService(services.GetRequiredService<IServiceScopeFactory>(), services.GetRequiredService<DiscoveryRuntimeOptions>(),
            TimeProvider.System, NullLogger<DiscoveryJobService>.Instance);
        await runner.StartAsync(default);
        try {
            await acquiring.Task.WaitAsync(TimeSpan.FromSeconds(8));
            lock (gate) queue.Add(Create(ready));
            await completed[ready.Id].Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(completed[slow.Id].Task.IsCompleted);
            release.TrySetResult();
            await completed[slow.Id].Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { release.TrySetResult(); await runner.StopAsync(default); }
    }

    private static PreparedDiscoveryPool Pool()
    {
        var response = new ResponseReference("listenbrainz", "fixture", "jazz", EvidenceOrigin.Synthetic, new string('a', 64), DateTimeOffset.UnixEpoch);
        var path = new DiscoveryPath("jazz", new(1, "1", null), null, response, 1, null, null, new(null, null, "not_supplied"));
        var candidate = new HydratedCandidateTrack(new Track("Song", "Artist"), WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["jazz"] = 1 }),
            new("Song", "Artist", null, [path]), [], response, null);
        return new(new string('a', 64), "listenbrainz", EvidenceOrigin.Synthetic, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5),
            CatalogText.Version, [candidate], [], 1000, new string('b', 64));
    }
    private sealed class Admission : IDiscoveryWriteCoordinator
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        public bool Active;
        public async Task<T> ExecuteAsync<T>(long bytes, Func<CancellationToken, Task<T>> write, CancellationToken ct)
        {
            await gate.WaitAsync(ct); Active = true;
            try { return await write(ct); }
            finally { Active = false; gate.Release(); }
        }
    }
}
