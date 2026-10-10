using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Digests.Commands.GenerateDigest;
using LinerNotes.Application.Features.Digests.Commands.RecordFeedback;
using LinerNotes.Application.Features.Export.Queries.GetUserDataExport;
using LinerNotes.Application.Features.Subscribers.Commands.DeleteUserAccount;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using LinerNotes.Infrastructure.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace LinerNotes.DataAccess.Tests;

public sealed class Phase6PostgresTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly Guid userId = Guid.NewGuid();
    private readonly string databaseName = "liner_phase6_" + Guid.NewGuid().ToString("N");
    private readonly string root = Path.Combine(Path.GetTempPath(), "liner-phase6-db-" + Guid.NewGuid().ToString("N"));
    private ServiceProvider services = null!;
    private string admin = "";
    private string connection = "";
    private readonly GenerationStorageOptions storageOptions = new();
    private static readonly IsoWeek Week = new(2026, 41);

    public async Task InitializeAsync()
    {
        admin = Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES") ?? "";
        if (admin.Length == 0) return;
        var builder = new NpgsqlConnectionStringBuilder(admin);
        if (builder.Host is not ("localhost" or "127.0.0.1" or "::1")) throw new InvalidOperationException("Local database required.");
        await using (var db = new NpgsqlConnection(admin))
        {
            await db.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", db);
            await command.ExecuteNonQueryAsync();
        }
        builder.Database = databaseName; connection = builder.ConnectionString;
        Directory.CreateDirectory(root);
        storageOptions.InventoryPath = Path.Combine(root, "inventory.json");
        storageOptions.LeasePath = Path.Combine(root, "lease");
        storageOptions.DatabaseOverheadBytesPerPick = 1_000_000; // Test bound only; production remains unconfigured.
        storageOptions.ArtifactRoots = LocalGenerationStorage.Categories.ToDictionary(c => c, _ => Array.Empty<string>());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = connection, ["Generation:Origin"] = "Recorded",
            ["Generation:RecordingExpiresAtUtc"] = DateTimeOffset.UtcNow.AddDays(1).ToString("O"),
            ["Generation:ApprovedRecordingManifestSha256"] = new string('a', 64) }).Build();
        var registrations = new ServiceCollection().AddLogging().AddApplication().AddDataAccess(config).AddInfrastructure(config);
        registrations.AddSingleton(storageOptions);
        foreach (var recording in Recordings()) registrations.AddSingleton(recording);
        services = registrations.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        await context.Database.EnsureCreatedAsync();
        context.ApplicationUsers.Add(new ApplicationUser { Id = userId, UserName = "phase6", Email = "phase6@example.test" });
        context.Users.Add(new User("phase6@example.test", "UTC", id: userId));
        context.TasteSignals.Add(TasteSignal.CreateSeedArtist(userId, "Seed Artist", 1, "manual"));
        context.TasteSignals.Add(TasteSignal.CreateSeedTag(userId, "rock", 0.5, "manual"));
        await context.SaveChangesAsync();
        await ReconcileAsync();
    }

    public async Task DisposeAsync()
    {
        if (services is not null) await services.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        if (admin.Length > 0)
        {
            await using var db = new NpgsqlConnection(admin); await db.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", db);
            await command.ExecuteNonQueryAsync();
        }
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private async Task ReconcileAsync()
    {
        await using var scope = services.CreateAsyncScope();
        var state = await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync();
        await new LocalGenerationStorage(storageOptions, TimeProvider.System).ReconcileAsync(state);
    }

    private async Task<GenerateDigestResult> GenerateAsync(IsoWeek? week = null)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GenerateDigestCommand(userId, week ?? Week));
    }

    [LocalPostgresMigrationFact]
    public async Task Generation_PersistsReplayableEvidenceCanonicalWeekAndMeasuredSizes()
    {
        var result = await GenerateAsync();
        Assert.Equal("generated", result.Status); Assert.Null(result.Reason);
        Assert.Equal(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc), result.Digest!.WeekStartDate);
        Assert.Equal(2, result.Digest.Recommendations.Count);
        Assert.Contains(result.Gaps, g => g.Reason == "no_valid_descriptive_tag_counts");
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var picks = await db.WeeklyRecommendations.Include(r => r.Track).OrderBy(r => r.Rank).ToArrayAsync();
        var selected = picks.Single(r => r.Track.ArtistName == "Candidate Artist");
        Assert.Equal(2, selected.ScoreBreakdown.Snapshot!.Evidence.DiscoveryPaths.Count);
        Assert.Equal(2, selected.ScoreBreakdown.Snapshot.Evidence.Signals.Count);
        Assert.All(picks, pick =>
        {
            Assert.NotNull(pick.ScoreBreakdown.Snapshot);
            Assert.Equal(pick.ScoreBreakdown.FinalScore, new BaselineAScorer().Replay(pick.ScoreBreakdown.Snapshot!).FinalScore);
            Assert.Equal(0, pick.ScoreBreakdown.PopularityPenalty);
            Assert.All(pick.ScoreBreakdown.Snapshot!.Evidence.DiscoveryPaths, p => Assert.Equal("Recorded", p.TrackResponse.Origin));
        });
        var sizes = await db.Database.SqlQueryRaw<long>("""
            SELECT pg_column_size("ScoreBreakdown")::bigint AS "Value" FROM "WeeklyRecommendations" ORDER BY "Rank"
            """).ToArrayAsync();
        var sorted = sizes.Order().ToArray();
        double median = (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2.0;
        output.WriteLine($"pg_column_size bytes: median={median}, p95={sorted[(int)Math.Ceiling(sorted.Length * .95) - 1]}, max={sorted.Max()}, total={sorted.Sum()}");
        output.WriteLine($"UTF8 breakdown bytes: {string.Join(',', picks.Select(p => JsonSerializer.SerializeToUtf8Bytes(p.ScoreBreakdown).Length))}");
        Assert.All(sizes, size => Assert.InRange(size, 1, 100_000));
    }

    [LocalPostgresMigrationFact]
    public async Task ConcurrentGenerationAndRetry_ReturnExactlyOneDigest()
    {
        var results = await Task.WhenAll(GenerateAsync(), GenerateAsync());
        Assert.Contains(results, r => r.Status == "generated");
        Assert.Contains(results, r => r.Status == "existing");
        Assert.Equal(results[0].Digest!.Id, results[1].Digest!.Id);
        Assert.Equal("existing", (await GenerateAsync()).Status); // Does not need a refreshed inventory or responses.
        await using var scope = services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<DataContext>().WeeklyDigests.CountAsync());
    }

    [LocalPostgresMigrationFact]
    public async Task StoredByteFailureAndCancellation_RollBackDigestAndCatalog()
    {
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>();
        await using var batch = await store.AcquireBatchAsync();
        var input = await store.ReadInputsAsync(userId);
        var track = Track.Create("Rollback", "Rollback Artist");
        var score = new BaselineAScorer().Score(new(track, WeightedTagVector.Empty, ScoreEvidence.Empty), WeightedTagVector.Empty, false, Week, new());
        await Assert.ThrowsAsync<GenerationStoppedException>(() => store.PersistAsync(userId, Week, input.Revision,
            [new(track, score)], new FailedReservation()));
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal(0, await db.WeeklyDigests.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Tracks.CountAsync());
        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.PersistAsync(userId, Week, input.Revision,
            [new(Track.Create("Cancel", "Cancel Artist"), score)], new CancelReservation(cts), cts.Token));
        Assert.Equal(0, await db.WeeklyRecommendations.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Tracks.CountAsync());
    }

    [LocalPostgresMigrationFact]
    public async Task ChangedTasteAndInsufficientOverhead_StopWithoutPartialWrites()
    {
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>();
        await using (var batch = await store.AcquireBatchAsync())
        {
            var input = await store.ReadInputsAsync(userId);
            await using var change = services.CreateAsyncScope();
            var changed = change.ServiceProvider.GetRequiredService<DataContext>();
            changed.TasteSignals.Add(TasteSignal.CreateSeedTag(userId, "extra", 1, "changed while acquiring"));
            await changed.SaveChangesAsync();
            var ex = await Assert.ThrowsAsync<GenerationStoppedException>(() => store.PersistAsync(userId, Week, input.Revision, [], new FailedReservation()));
            Assert.Equal("taste_revision_changed", ex.Message);
        }
        storageOptions.DatabaseOverheadBytesPerPick = 1;
        await ReconcileAsync();
        var result = await GenerateAsync();
        Assert.Equal("stopped", result.Status);
        Assert.Equal("stored_bytes_exceed_reservation", result.Reason);
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DataContext>().WeeklyDigests.CountAsync());
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DataContext>().Tracks.CountAsync());
    }

    [LocalPostgresMigrationFact]
    public async Task LegacyFutureAndNestedUnknownJson_RoundTripAndExportWithoutInventedEvidence()
    {
        var generated = await GenerateAsync(); Assert.Equal("generated", generated.Status);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var pick = await db.WeeklyRecommendations.OrderBy(r => r.Rank).FirstAsync();
        var json = JsonSerializer.SerializeToNode(pick.ScoreBreakdown)!;
        json["futureRoot"] = new JsonObject { ["value"] = 42 };
        json["MatchedTags"]![0]!["futureLegacyTag"] = "retained";
        var snapshot = json["Snapshot"]!;
        snapshot["futureSnapshot"] = true;
        snapshot["Weights"]!["futureWeights"] = "retained";
        snapshot["Contributions"]![0]!["futureContribution"] = 1;
        snapshot["Components"]![0]!["futureComponent"] = 2;
        snapshot["Evidence"]!["futureEvidence"] = 3;
        snapshot["Evidence"]!["Seeds"]![0]!["futureSeed"] = 4;
        snapshot["Evidence"]!["CandidateTags"]![0]!["futureTag"] = 5;
        snapshot["Evidence"]!["CandidateTags"]![0]!["Count"]!["futureCount"] = 6;
        snapshot["Evidence"]!["CandidateTagResponse"]!["futureResponse"] = 7;
        snapshot["Evidence"]!["DiscoveryPaths"]![0]!["futurePath"] = 8;
        var text = json.ToJsonString();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WeeklyRecommendations\" SET \"ScoreBreakdown\" = CAST({text} AS jsonb) WHERE \"Id\" = {pick.Id}");
        var loaded = await db.WeeklyRecommendations.FirstAsync(r => r.Id == pick.Id);
        Assert.True(JsonNode.DeepEquals(json, JsonSerializer.SerializeToNode(loaded.ScoreBreakdown)));
        var export = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetUserDataExportQuery(userId));
        var exported = JsonSerializer.SerializeToNode(export.Digests.Single().Recommendations.Single(r => r.Id == pick.Id).ScoreBreakdown)!;
        Assert.NotNull(exported["futureRoot"]);
        Assert.NotNull(exported["Snapshot"]!["Evidence"]!["DiscoveryPaths"]![0]!["futurePath"]);
        var future = loaded.ScoreBreakdown.Snapshot! with { FormulaVersion = "future-formula" };
        var futureText = JsonSerializer.Serialize(loaded.ScoreBreakdown with { Snapshot = future });
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WeeklyRecommendations\" SET \"ScoreBreakdown\" = CAST({futureText} AS jsonb) WHERE \"Id\" = {pick.Id}");
        var inspectable = await db.WeeklyRecommendations.FirstAsync(r => r.Id == pick.Id);
        Assert.Throws<NotSupportedException>(() => new BaselineAScorer().Replay(inspectable.ScoreBreakdown.Snapshot!));
        const string legacy = """{"FinalScore":0.5,"MatchedTags":[{"TagName":"rock","CandidateTagWeight":1,"UserTasteWeight":0.5,"ContributionProduct":0.5}],"futureLegacy":9}""";
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WeeklyRecommendations\" SET \"ScoreBreakdown\" = CAST({legacy} AS jsonb) WHERE \"Id\" = {pick.Id}");
        var old = await db.WeeklyRecommendations.FirstAsync(r => r.Id == pick.Id);
        Assert.Null(old.ScoreBreakdown.Snapshot);
        Assert.Equal(0.5, Assert.Single(old.ScoreBreakdown.MatchedTags).ContributionProduct);
        Assert.Equal(9, old.ScoreBreakdown.UnknownFields!["futureLegacy"].GetInt32());
    }

    [LocalPostgresMigrationFact]
    public async Task Feedback_BothOrderingDirectionsPreserveFamiliarityAndLatestRating()
    {
        var generated = await GenerateAsync(); Assert.Equal("generated", generated.Status);
        var recId = generated.Digest!.Recommendations[0].Id;
        async Task Feedback(UserFeedback category, int? rating = null)
        {
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RecordRecommendationFeedbackCommand(recId, userId, category, Rating: rating));
        }
        await Feedback(UserFeedback.None, 9);
        await Feedback(UserFeedback.AlreadyKnown); // Keeps the earlier numeric rating.
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            Assert.Equal(9, (await db.WeeklyRecommendations.SingleAsync(r => r.Id == recId)).Rating);
            Assert.Contains(await db.TasteSignals.ToArrayAsync(), s => s.Source == TasteSignalSource.RecommendationAlreadyKnown);
        }
        await Feedback(UserFeedback.None, 2); // Keeps familiarity, changes sentiment.
        await using (var scope = services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>();
            var inputs = await store.ReadInputsAsync(userId);
            Assert.Contains(inputs.Signals, s => s.Source == TasteSignalSource.RecommendationAlreadyKnown);
            Assert.Equal(UserFeedback.AlreadyKnown, inputs.Feedback.Single(r => r.Id == recId).Feedback);
            var effective = EffectiveTasteInputs.From(userId, inputs.Signals, inputs.Feedback, new());
            Assert.Contains("candidate artist:a track", effective.ExcludedAliases);
            Assert.DoesNotContain(effective.PositiveSignals, s => s.TargetValue == "Candidate Artist");
        }
        await Feedback(UserFeedback.None); // Explicit reset clears familiarity and the rating.
        await using var verify = services.CreateAsyncScope();
        var signals = await verify.ServiceProvider.GetRequiredService<DataContext>().TasteSignals.ToArrayAsync();
        Assert.DoesNotContain(signals, s => s.Context.StartsWith($"rec:{recId} - ", StringComparison.Ordinal));
    }

    [LocalPostgresMigrationFact]
    public async Task Export_PagesAllHistoryIncludingTimestampTiesAndStoredAuditFields()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var start = new DateTime(2000, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 1005; i++)
        {
            var date = start.AddDays(i * 7);
            db.WeeklyDigests.Add(new(userId, new(System.Globalization.ISOWeek.GetYear(date), System.Globalization.ISOWeek.GetWeekOfYear(date)), date, date.AddDays(7)));
        }
        await db.SaveChangesAsync(); // Equal audit timestamps exercise the identifier cursor.
        var deletedSignal = TasteSignal.CreateSeedTag(userId, "deleted-but-held", 1, "export inspection");
        deletedSignal.IsDeleted = true; deletedSignal.DeletedAt = DateTime.UtcNow; deletedSignal.DeletedBy = userId;
        db.TasteSignals.Add(deletedSignal);
        var deletedConnection = new UserMusicConnection(userId, MusicServiceType.LastFm, "held-for-export")
        { IsDeleted = true, DeletedAt = DateTime.UtcNow, DeletedBy = userId };
        db.UserMusicConnections.Add(deletedConnection);
        await db.SaveChangesAsync();
        var export = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetUserDataExportQuery(userId));
        Assert.Equal("2.1", export.ExportVersion);
        Assert.Equal(1005, export.Digests.Count);
        Assert.Equal(1005, export.Digests.Select(d => d.Id).Distinct().Count());
        Assert.All(export.Digests, d => Assert.True(d.CreatedAt > DateTime.MinValue));
        Assert.All(export.TasteSignals, s => Assert.NotEqual(Guid.Empty, s.Id));
        Assert.Contains(export.TasteSignals, s => s.Id == deletedSignal.Id && s.IsDeleted && s.DeletedBy == userId);
        Assert.Contains(export.Connections, c => c.Id == deletedConnection.Id && c.IsDeleted && c.UserId == userId);
        var json = JsonSerializer.Serialize(export);
        Assert.DoesNotContain("EncryptedToken", json); Assert.DoesNotContain("PasswordHash", json);
    }

    [LocalPostgresMigrationFact]
    public async Task AccountDeletion_RemovesPhase6SnapshotsAndKeepsSharedCatalogBoundary()
    {
        Assert.Equal("generated", (await GenerateAsync()).Status);
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new DeleteUserAccountCommand(userId));
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal(0, await db.WeeklyRecommendations.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.WeeklyDigests.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.TasteSignals.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Users.IgnoreQueryFilters().CountAsync());
        Assert.Equal(2, await db.Tracks.CountAsync());
    }

    [LocalPostgresMigrationFact]
    public async Task StorageRevision_HashesRowsSeparatelyAndChangesWhenStoredContentChanges()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var store = scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>();
        var before = await store.ReadStorageAsync();
        Assert.Equal(before, await store.ReadStorageAsync());
        db.Tracks.Add(Track.Create("Storage revision probe", "Revision Artist"));
        await db.SaveChangesAsync();
        var after = await store.ReadStorageAsync();
        Assert.NotEqual(before.Revision, after.Revision);
        Assert.Equal(after, await store.ReadStorageAsync());
        Assert.True(after.DatabaseBytes >= before.DatabaseBytes);
    }

    [LocalPostgresMigrationFact]
    public async Task StorageStop_LeavesExistingDigestFeedbackExportAndDeletionAvailable()
    {
        var generated = await GenerateAsync(); Assert.Equal("generated", generated.Status);
        File.Delete(storageOptions.InventoryPath!);
        var stopped = await GenerateAsync(new(2026, 42));
        Assert.Equal("stopped", stopped.Status);
        Assert.Equal("unknown_storage_inventory", stopped.Reason);
        Assert.Equal("existing", (await GenerateAsync()).Status);
        await using var scope = services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Assert.True(await sender.Send(new RecordRecommendationFeedbackCommand(
            generated.Digest!.Recommendations[0].Id, userId, UserFeedback.Liked, Rating: 9)));
        Assert.Single((await sender.Send(new GetUserDataExportQuery(userId))).Digests);
        Assert.True(await sender.Send(new DeleteUserAccountCommand(userId)));
    }

    [Fact]
    public void EfModel_StillMatchesExistingSqlMigrationSnapshot()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").Options;
        using var db = new AppDbContext(options);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static IEnumerable<RecordedLastFmResponse> Recordings()
    {
        static RecordedLastFmResponse Input(string method, string identity, string json, int limit)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            return new(method, identity, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)), DateTimeOffset.UnixEpoch, limit);
        }
        yield return Input("artist.gettoptags", "Seed Artist", """{"toptags":{"tag":[{"name":"rock","count":"100"},{"name":"ambient","count":"50"}]}}""", 15);
        yield return Input("artist.getsimilar", "Seed Artist", """{"similarartists":{"artist":[{"name":"Candidate Artist","match":"0.8"},{"name":"Empty Artist","match":"0.6"}]}}""", 10);
        yield return Input("artist.gettoptracks", "Candidate Artist", """{"toptracks":{"track":[{"name":"A Track","mbid":"candidate-id","artist":{"name":"Candidate Artist"}},{"name":"B Track","mbid":"z-candidate-id","artist":{"name":"Candidate Artist"}}]}}""", 2);
        yield return Input("artist.gettoptracks", "Empty Artist", """{"toptracks":{"track":[{"name":"Empty Track","artist":{"name":"Empty Artist"}}]}}""", 2);
        yield return Input("tag.gettoptracks", "rock", """{"tracks":{"track":[{"name":"A Track","artist":{"name":"Candidate Artist"}}]}}""", 10);
        yield return Input("artist.gettoptags", "Candidate Artist", """{"toptags":{"tag":[{"name":"rock","count":"100"},{"name":"ambient","count":"30"},{"name":"seen live","count":"NaN"}]}}""", 15);
        yield return Input("artist.gettoptags", "Empty Artist", """{"toptags":{"tag":[]}}""", 15);
    }

    private sealed class FailedReservation : IGenerationStorageReservation
    {
        public Task ValidateAsync(GenerationStorageState database, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task VerifyStoredAsync(long actualColumnBytes, GenerationStorageState database, CancellationToken cancellationToken = default) =>
            throw new GenerationStoppedException("injected_stored_size_failure");
    }
    private sealed class CancelReservation(CancellationTokenSource cancellation) : IGenerationStorageReservation
    {
        public Task ValidateAsync(GenerationStorageState database, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task VerifyStoredAsync(long actualColumnBytes, GenerationStorageState database, CancellationToken cancellationToken = default)
        { cancellation.Cancel(); cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }
    }
}
