using System.Text.Json;
using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.DataAccess.Persistence.Repositories;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class DiscoveryMigrationTests : IAsyncLifetime
{
    private readonly string _name = "liner_discovery_c2_" + Guid.NewGuid().ToString("N");
    private readonly Guid _owner = Guid.NewGuid(), _other = Guid.NewGuid(), _hidden = Guid.NewGuid();
    private readonly Guid[] _digests = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
    private readonly Guid _recommendation = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    private string _admin = "", _connection = "", _scoreBefore = "";
    private AppDbContext _db = null!;
    private Guid _track;

    public async Task InitializeAsync()
    {
        _admin = Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES") ?? "";
        if (_admin.Length == 0) return;
        var settings = new NpgsqlConnectionStringBuilder(_admin);
        Assert.Contains(settings.Host, new[] { "127.0.0.1", "localhost", "::1" });
        await using (var admin = new NpgsqlConnection(_admin))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_name}\"", admin);
            await command.ExecuteNonQueryAsync();
        }
        settings.Database = _name;
        settings.Pooling = false;
        _connection = settings.ConnectionString;
        _db = new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connection).Options);
        await _db.Database.MigrateAsync("20261010205812_AddEmailUnsubscribedAtUtc");
        foreach (var id in new[] { _owner, _other, _hidden })
        {
            _db.ApplicationUsers.Add(new ApplicationUser
            {
                Id = id,
                UserName = $"fixture-{id:N}",
                NormalizedUserName = $"FIXTURE-{id:N}",
                Email = $"fixture-{id:N}@example.test"
            });

        }
        var track = Track.Create("Migration fixture recording", "Synthetic migration artist");
        _track = track.Id;
        _db.Tracks.Add(track);

        await _db.SaveChangesAsync();
        foreach (var id in new[] { _owner, _other, _hidden })
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Users" ("Id","Email","TimeZone","DeliveryDay","DeliveryHourUtc","CreatedAt","IsDeleted")
                VALUES ({id},{$"fixture-{id:N}@example.test"},{"UTC"},{"Sunday"},{8},{Now.UtcDateTime},{false})
                """);
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TasteSignals" ("Id","UserId","TargetType","TargetValue","NormalizedTargetValue","Weight","Source","Context","CreatedAt","IsDeleted")
            VALUES ({Guid.NewGuid()},{_owner},{"Tag"},{"jazz"},{"jazz"},{0.75},{"InitialSeedManual"},{"Migration fixture"},{Now.UtcDateTime},{false})
            """);
        await _db.Users.Where(u => u.Id == _hidden).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsDeleted, true));

        for (var i = 0; i < _digests.Length; i++)
        {
            var owner = i == 2 ? _other : _owner;
            var week = i == 1 ? "2026-W40" : "2026-W41";
            var start = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc).AddDays(i == 1 ? -7 : 0);
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "WeeklyDigests" ("Id","UserId","Week","WeekStartDate","WeekEndDate","Status","CreatedAt","IsDeleted")
                VALUES ({_digests[i]},{owner},{week},{start},{start.AddDays(7)},{"Pending"},{start},{false})
                """);
        }
        var score = new ScoreBreakdown
        {
            FinalScore = 0.75,
            UnknownFields = new() { ["futurePreserved"] = JsonSerializer.SerializeToElement(new { marker = "keep", value = 37 }) }
        };
        var json = JsonSerializer.Serialize(score);
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "WeeklyRecommendations" ("Id","WeeklyDigestId","UserId","TrackId","Rank","ScoreBreakdown",
                "Feedback","Rating","FeedbackComment","FeedbackGivenAt","CreatedAt","IsDeleted")
            VALUES ({_recommendation},{_digests[0]},{_owner},{_track},{1},CAST({json} AS jsonb),
                {"Liked"},{9},{"preserve this feedback"},{Now.UtcDateTime},{Now.UtcDateTime},{false})
            """);
        _scoreBefore = await _db.Database.SqlQuery<string>($"SELECT \"ScoreBreakdown\"::text AS \"Value\" FROM \"WeeklyRecommendations\" WHERE \"Id\"={_recommendation}").SingleAsync();
        await _db.Database.MigrateAsync();
        _db.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        if (_admin.Length == 0) return;
        if (_db is not null) await _db.DisposeAsync();
        await using var connection = new NpgsqlConnection(_admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    [LocalPostgresMigrationFact]
    public async Task PopulatedBackfillPreservesIdentityFeedbackAndUnknownScoreFields()
    {
        Assert.Equal(3, await _db.ListenerProfiles.CountAsync());
        Assert.Equal(3, await _db.DiscoveryBatches.CountAsync());
        Assert.Equal(_owner, (await _db.ListenerProfiles.SingleAsync(p => p.UserId == _owner)).Id);
        Assert.NotNull(await _db.ListenerProfiles.SingleOrDefaultAsync(p => p.UserId == _hidden));
        var weeks = await _db.WeeklyDigests.OrderBy(d => d.Id).ToArrayAsync();
        Assert.All(weeks, d => Assert.Equal(d.Id, d.BatchId));
        var completed = await _db.DiscoveryBatches.SingleAsync(b => b.Id == _digests[0]);
        Assert.Equal(DiscoveryBatchKind.Weekly, completed.Kind);
        Assert.NotNull(completed.CompletedAtUtc);
        Assert.Null((await _db.DiscoveryBatches.SingleAsync(b => b.Id == _digests[1])).CompletedAtUtc);
        var recommendation = await _db.WeeklyRecommendations.SingleAsync(r => r.Id == _recommendation);
        Assert.Equal(9, recommendation.Rating);
        Assert.Equal("preserve this feedback", recommendation.FeedbackComment);
        Assert.Equal("keep", recommendation.ScoreBreakdown.UnknownFields!["futurePreserved"].GetProperty("marker").GetString());
        Assert.Equal(_scoreBefore, await _db.Database.SqlQuery<string>($"SELECT \"ScoreBreakdown\"::text AS \"Value\" FROM \"WeeklyRecommendations\" WHERE \"Id\"={_recommendation}").SingleAsync());
        var reader = new DiscoveryProfileReader(_db);
        var exported = await reader.GetForAccountAsync(_owner);
        Assert.Equal(_owner, exported.Profile!.UserId);
        Assert.Equal(2, exported.Batches.Count);
        Assert.All(exported.Batches, b => Assert.Equal(_owner, b.ProfileId));
        Assert.Null((await reader.GetForAccountAsync(_hidden)).Profile);
        Assert.Null((await reader.GetForAccountAsync(Guid.NewGuid())).Profile);
    }

    [LocalPostgresMigrationFact]
    public async Task DatabaseConstrainsUnfinishedJobsAndCrossProfileResults()
    {
        var first = ListenerProfile.Guest(Now, TimeSpan.FromDays(7));
        var other = ListenerProfile.Guest(Now, TimeSpan.FromDays(7));
        var batch = new DiscoveryBatch(other.Id, "other-request", DiscoveryBatchKind.Initial, 0, Now);
        _db.ListenerProfiles.AddRange(first, other);
        _db.DiscoveryBatches.Add(batch);
        var job = Job(first.Id, "first", DiscoveryJobKind.Generation);
        _db.DiscoveryJobs.AddRange(job, Job(first.Id, "lookup", DiscoveryJobKind.Lookup));
        await _db.SaveChangesAsync();
        _db.DiscoveryJobs.Add(Job(first.Id, "second", DiscoveryJobKind.Generation));
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
        _db.ChangeTracker.Clear();
        var wrongOwner = await Assert.ThrowsAsync<PostgresException>(() => _db.DiscoveryJobs.Where(j => j.Id == job.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.ResultBatchId, batch.Id)));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, wrongOwner.SqlState);
        Assert.Null((await _db.DiscoveryJobs.SingleAsync(j => j.Id == job.Id)).ResultBatchId);
    }

    [LocalPostgresMigrationFact]
    public async Task AdditiveDowngradeAndReapplyPreserveTheLegacyData()
    {
        await _db.Database.MigrateAsync("20261010205812_AddEmailUnsubscribedAtUtc");
        Assert.Equal(_scoreBefore, await _db.Database.SqlQuery<string>($"SELECT \"ScoreBreakdown\"::text AS \"Value\" FROM \"WeeklyRecommendations\" WHERE \"Id\"={_recommendation}").SingleAsync());
        Assert.Equal(3, await _db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM \"WeeklyDigests\"").SingleAsync());
        await _db.Database.MigrateAsync();
        Assert.Equal(3, await _db.ListenerProfiles.CountAsync());
        Assert.Equal(3, await _db.DiscoveryBatches.CountAsync());
        Assert.All(await _db.WeeklyDigests.ToArrayAsync(), d => Assert.Equal(d.Id, d.BatchId));
    }

    [LocalPostgresMigrationFact]
    public async Task ExportCoversNewStoredValuesAndAccountDeletionCascadesOnlyOwnedData()
    {
        var job = Job(_owner, "export-owner", DiscoveryJobKind.Generation);
        var other = Job(_other, "other-private-request", DiscoveryJobKind.Lookup);
        _db.DiscoveryJobs.AddRange(job, other);
        await _db.SaveChangesAsync();
        var token = Guid.NewGuid();
        await _db.DiscoveryJobs.Where(j => j.Id == job.Id).ExecuteUpdateAsync(s => s
            .SetProperty(j => j.LeaseToken, token).SetProperty(j => j.LeaseExpiresAtUtc, Now.AddMinutes(2))
            .SetProperty(j => j.State, DiscoveryJobState.Running).SetProperty(j => j.Attempts, 1)
            .SetProperty(j => j.ResultBatchId, _digests[0]).SetProperty(j => j.ResultJson, "{\"fixture\":[1,2]}"));
        var export = await new DiscoveryProfileReader(_db).GetForAccountAsync(_owner);
        var element = JsonSerializer.SerializeToElement(export, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain(token.ToString(), element.ToString());
        Assert.DoesNotContain("other-private-request", element.ToString());
        Assert.False(element.GetProperty("jobs")[0].TryGetProperty("leaseToken", out _));
        await AssertStoredRowAsync("ListenerProfiles", _owner, element.GetProperty("profile"));
        foreach (var batch in element.GetProperty("batches").EnumerateArray())
            await AssertStoredRowAsync("DiscoveryBatches", batch.GetProperty("id").GetGuid(), batch);
        await AssertStoredRowAsync("DiscoveryJobs", job.Id, element.GetProperty("jobs")[0], ["LeaseToken"]);

        await using (var transaction = await _db.Database.BeginTransactionAsync())
        {
            Assert.True(await new UserRepository(_db).DeleteOwnedDataAsync(_owner));
            await transaction.CommitAsync();
        }
        Assert.False(await _db.ListenerProfiles.AnyAsync(p => p.UserId == _owner));
        Assert.False(await _db.DiscoveryBatches.AnyAsync(b => b.ProfileId == _owner));
        Assert.False(await _db.DiscoveryJobs.AnyAsync(j => j.ProfileId == _owner));
        Assert.True(await _db.DiscoveryJobs.AnyAsync(j => j.Id == other.Id));
        Assert.True(await _db.Tracks.AnyAsync(t => t.Id == _track));
    }

    [LocalPostgresMigrationFact]
    public async Task IndependentBatchesRemainPrivateAndAccountAdaptersResolveNonAccountProfileIds()
    {
        var profile = ListenerProfile.Guest(Now, TimeSpan.FromDays(7));
        var first = new DiscoveryBatch(profile.Id, "initial-request", DiscoveryBatchKind.Initial, 0, Now);
        var second = new DiscoveryBatch(profile.Id, "next-request", DiscoveryBatchKind.OnDemand, 0, Now);
        var track = await _db.Tracks.SingleAsync(t => t.Id == _track);
        var pick = WeeklyRecommendation.ForDiscovery(profile.Id, first.Id, null, track, 1, new());
        var signal = TasteSignal.ForProfile(profile.Id, null, LinerNotes.Domain.Enums.TasteTargetType.Tag,
            "jazz", 1, LinerNotes.Domain.Enums.TasteSignalSource.InitialSeedManual, "Guest choice");
        _db.ListenerProfiles.Add(profile);
        _db.DiscoveryBatches.AddRange(first, second);
        _db.WeeklyRecommendations.Add(pick);
        _db.TasteSignals.Add(signal);
        _db.Entry(track).State = EntityState.Unchanged;
        await _db.SaveChangesAsync();
        Assert.Equal(2, await _db.DiscoveryBatches.CountAsync(b => b.ProfileId == profile.Id));
        Assert.Null(await new WeeklyDigestRepository(_db).GetRecommendationByIdAsync(pick.Id, _owner));

        var account = Guid.NewGuid();
        _db.ApplicationUsers.Add(new ApplicationUser { Id = account, UserName = "claimed-fixture", Email = "claimed@example.test" });
        _db.Users.Add(new User("claimed@example.test", id: account, createProfile: false));
        profile.Attach(account, Now.AddMinutes(1));
        _db.ListenerProfiles.Update(profile);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var repo = new TasteSignalRepository(_db);
        Assert.Equal(profile.Id, Assert.Single(await repo.GetByUserIdAsync(account)).ProfileId);
        await repo.AddRangeAsync([TasteSignal.CreateSeedArtist(account, "Another synthetic artist", 1, "Legacy adapter")]);
        await _db.SaveChangesAsync();
        Assert.All(await repo.GetByUserIdAsync(account), row => Assert.Equal(profile.Id, row.ProfileId));
        Assert.NotNull(await new WeeklyDigestRepository(_db).GetRecommendationByIdAsync(pick.Id, account));
        var export = await new DiscoveryProfileReader(_db).GetForAccountAsync(account);
        Assert.Equal(pick.Id, Assert.Single(export.Recommendations!).Id);
        Assert.Equal(profile.Id, Assert.Single(export.Recommendations!).ProfileId);
        Assert.Null(Assert.Single(export.Recommendations!).WeeklyDigestId);
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            Assert.True(await new UserRepository(_db).DeleteOwnedDataAsync(account));
            await tx.CommitAsync();
        }
        Assert.False(await _db.WeeklyRecommendations.IgnoreQueryFilters().AnyAsync(r => r.Id == pick.Id));
        Assert.True(await _db.Tracks.AnyAsync(t => t.Id == _track));
        Assert.True(await _db.ListenerProfiles.AnyAsync(p => p.UserId == _owner));
    }

    private static DiscoveryJob Job(Guid owner, string key, DiscoveryJobKind kind) =>
        new(owner, key, new string('a', 64), kind, 0, "{\"manual\":\"synthetic owner input\"}", Now);

    private async Task AssertStoredRowAsync(string table, Guid id, JsonElement exported, string[]? excluded = null)
    {
        await using var connection = new NpgsqlConnection(_connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT to_jsonb(t)::text FROM \"{table}\" t WHERE \"Id\"=@id", connection);
        command.Parameters.AddWithValue("id", id);
        using var stored = JsonDocument.Parse((string)(await command.ExecuteScalarAsync())!);
        foreach (var property in stored.RootElement.EnumerateObject())
        {
            if (excluded?.Contains(property.Name) == true) continue;
            var actual = exported.GetProperty(JsonNamingPolicy.CamelCase.ConvertName(property.Name));
            AssertValue(property.Value, actual);
        }
    }

    private static void AssertValue(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind == JsonValueKind.Object)
            foreach (var property in expected.EnumerateObject()) AssertValue(property.Value, actual.GetProperty(property.Name));
        else if (expected.ValueKind == JsonValueKind.Array)
        {
            Assert.Equal(expected.GetArrayLength(), actual.GetArrayLength());
            for (var i = 0; i < expected.GetArrayLength(); i++) AssertValue(expected[i], actual[i]);
        }
        else if (expected.ValueKind == JsonValueKind.String && expected.TryGetDateTimeOffset(out var date))
            Assert.Equal(date, actual.GetDateTimeOffset());
        else if (expected.ValueKind == JsonValueKind.Number) Assert.Equal(expected.GetDecimal(), actual.GetDecimal());
        else Assert.Equal(expected.ToString(), actual.ToString());
    }
}
