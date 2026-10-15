using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Digests.Commands.GenerateDigest;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace LinerNotes.Infrastructure.Tests;

public sealed class ListenBrainzProviderTests
{
    private const string Artist = "11111111-1111-4111-8111-111111111111";
    private const string OtherArtist = "22222222-2222-4222-8222-222222222222";

    private static ListenBrainzResponse Response(ListenBrainzRequestKind kind, string identity, string json) =>
        ListenBrainzResponse.Parse(new(kind, identity), Encoding.UTF8.GetBytes(json), EvidenceOrigin.Recorded, DateTimeOffset.UnixEpoch);

    [Theory]
    [InlineData("{\"count\":0,\"artists\":[]}", "artist_not_resolved")]
    [InlineData("{\"count\":2,\"artists\":[{\"id\":\"11111111-1111-4111-8111-111111111111\",\"name\":\"Example\"},{\"id\":\"22222222-2222-4222-8222-222222222222\",\"name\":\"Example\"}]}", "artist_ambiguous")]
    [InlineData("{\"count\":30,\"artists\":[{\"id\":\"11111111-1111-4111-8111-111111111111\",\"name\":\"Example\"}]}", "artist_search_incomplete")]
    [InlineData("{\"count\":\"bad\",\"artists\":[]}", "artist_not_resolved")]
    public void ArtistSearchDoesNotGuessOrTrustIncompleteResults(string json, string expected)
    {
        var response = Response(ListenBrainzRequestKind.ArtistSearch, "Example", json);
        Assert.Null(ListenBrainzEvidenceParser.ResolveArtist(response, "Example", out var reason));
        Assert.Equal(expected, reason);
    }

    [Fact]
    public void SuppliedAliasResolvesOneArtist()
    {
        var response = Response(ListenBrainzRequestKind.ArtistSearch, "Alias", "{\"count\":1,\"artists\":[{\"id\":\"" + Artist + "\",\"name\":\"Example\",\"aliases\":[{\"name\":\"Alias\"}]}]}");
        Assert.Equal(Artist, ListenBrainzEvidenceParser.ResolveArtist(response, "Alias", out var reason)!.Mbid);
        Assert.Null(reason);
    }

    [Fact]
    public void TagsKeepRawVotesArtistIdentityAndExclusions()
    {
        string json = JsonSerializer.Serialize(new[] { new { artist_mbid = Artist, tag = new { artist = new object[]
        {
            new { artist_mbid = Artist, tag = "Rock", count = 10 }, new { artist_mbid = Artist, tag = "rock", count = 5 },
            new { artist_mbid = Artist, tag = "electronic", count = 2 }, new { artist_mbid = Artist, tag = "seen live", count = 99 },
            new { artist_mbid = OtherArtist, tag = "wrong", count = 100 }, new { artist_mbid = Artist, tag = "invalid", count = -1 }
        } } } });
        var result = ListenBrainzEvidenceParser.ArtistTags([(Artist, Response(ListenBrainzRequestKind.ArtistMetadata, Artist, json))]);
        Assert.Equal(1, result.Vector["rock"]); Assert.Equal(.2, result.Vector["electronic"]);
        Assert.Equal(0, result.Vector["seen live"]); Assert.Equal(0, result.Vector["wrong"]);
        Assert.Contains(result.Tags, t => t.EntityIdentity == OtherArtist && t.ExclusionReason == "tag_artist_mismatch");
        Assert.Contains(result.Tags, t => t.Count.RawValue == "-1" && t.ExclusionReason == "invalid_tag_vote_count");
        Assert.Null(result.MissingReason);
    }

    [Fact]
    public void ArtistMetadataEnvelopeScopesTagsWhenRedundantInnerIdIsAbsent()
    {
        string json=JsonSerializer.Serialize(new[] { new { artist_mbid=Artist,tag=new {artist=new[]{new {tag="rock",count=3}}} } });
        var result=ListenBrainzEvidenceParser.ArtistTags([(Artist,Response(ListenBrainzRequestKind.ArtistMetadata,Artist,json))]);
        Assert.Equal(1,result.Vector["rock"]);Assert.Equal(Artist,Assert.Single(result.Tags).EntityIdentity);
    }

    [Fact]
    public async Task SelectedBundleNeverConstructsLastFmAndFixtureOriginCannotEnableHttp()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Generation:Provider"] = "listenbrainz", ["Generation:Origin"] = "Synthetic",
            ["LastFm:RequestsPerSecond"] = "-1", ["LastFm:BaseUrl"] = "invalid"
        }).Build();
        using var services = new ServiceCollection().AddLogging().AddInfrastructure(config).BuildServiceProvider();
        using var scope = services.CreateScope();
        var bundle = scope.ServiceProvider.GetRequiredService<IRecommendationProviderResolver>().Resolve();
        Assert.Equal("listenbrainz", bundle.Id);
        Assert.Same(bundle.Source, bundle.Hydrator); Assert.Same(bundle.Source, bundle.SeedTags);
        var candidates = await bundle.Source.GetCandidatesByArtistsAsync(["Synthetic Seed"]);
        Assert.Equal(5, candidates.Count); Assert.Empty(candidates.Gaps);
        Assert.All(candidates, c => Assert.Equal(EvidenceOrigin.Synthetic, c.Origin));
        Assert.All(candidates.SelectMany(c => c.Paths), p => Assert.Null(p.Match.Value));
        var hydrated = await bundle.Hydrator.HydrateCandidatesBatchAsync(candidates);
        Assert.Equal(5, hydrated.Count);
        Assert.All(hydrated, c => Assert.Equal(ListenBrainzEvidenceParser.NormalizationVersion, c.TagNormalizationVersion));
    }

    [Fact]
    public async Task ApprovedReplayRunsWholeGenerationAndPreservesSupportingEvidence()
    {
        var requests = new CollectingClient();
        var fixture = new ListenBrainzRecommendationProvider(requests);
        await fixture.GetAsync("Synthetic Seed");
        var byArtist = await fixture.GetCandidatesByArtistsAsync(["Synthetic Seed"]);
        var byTag = await fixture.GetCandidatesByTagsAsync(["rock"]);
        await fixture.HydrateCandidatesBatchAsync(byArtist.Concat(byTag).ToArray());
        var replay = new ListenBrainzRecommendationProvider(new RecordedListenBrainzApiClient(requests.Recordings));
        Guid user = Guid.NewGuid(); var week = new IsoWeek(2026, 42);
        var store = Substitute.For<IDigestGenerationStore>();
        var storage = Substitute.For<IGenerationStorage>();
        var lease = Substitute.For<IGenerationStorageLease>();
        var reservation = Substitute.For<IGenerationStorageReservation>();
        var config = new GenerationConfiguration { Provider = "listenbrainz", Origin = EvidenceOrigin.Recorded,
            RecordingExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1), ApprovedRecordingManifestSha256 = new string('a', 64) };
        store.AcquireBatchAsync(default).Returns(Substitute.For<IAsyncDisposable>());
        var signals = new[] { TasteSignal.CreateSeedArtist(user, "Synthetic Seed", 1, "seed"),
            TasteSignal.CreateSeedTag(user, "rock", 1, "seed") };
        store.ReadInputsAsync(user, default).Returns(new GenerationInputs(true, signals, [], "revision"));
        var database = new GenerationStorageState(1000, "db");
        store.ReadStorageAsync(default).Returns(database);
        storage.AcquireAsync(database, default).Returns(lease);
        lease.ReserveAsync(Arg.Any<long>(), Arg.Any<int>(), database, default).Returns(reservation);
        IReadOnlyList<BaselineAPick>? captured = null;
        store.PersistAsync(user, week, "revision", Arg.Any<IReadOnlyList<BaselineAPick>>(), Arg.Any<IGenerationStorageReservation>(), default)
            .Returns(call => { captured = call.ArgAt<IReadOnlyList<BaselineAPick>>(3); return WeeklyDigest.Create(user, week, DateTime.UtcNow, DateTime.UtcNow.AddDays(7)); });
        var handler = new GenerateDigestCommandHandler(store, replay, replay, replay, storage, config, new BaselineAScorer());
        var result = await handler.Handle(new(user, week), default);
        Assert.Equal("generated", result.Status); Assert.NotNull(captured); Assert.Equal(5, captured.Count);
        foreach (var pick in captured)
        {
            var snapshot = pick.Breakdown.Snapshot!;
            Assert.Equal("listenbrainz", snapshot.ConfigurationPolicy["Provider"]);
            Assert.Equal(ListenBrainzEvidenceParser.NormalizationVersion, snapshot.TagNormalizationVersion);
            Assert.Contains(snapshot.Evidence.DiscoveryPaths, p => p.SupportingResponses.Any(r => r.Provider == "MusicBrainz"));
            Assert.All(snapshot.Evidence.CandidateTags, t => Assert.NotNull(t.EntityIdentity));
            Assert.Equal(pick.Breakdown.FinalScore, new BaselineAScorer().Replay(snapshot).FinalScore);
        }
    }

    [Fact]
    public async Task MissingReplayIsAnExplicitGapAndNeverManufacturesCandidates()
    {
        var provider = new ListenBrainzRecommendationProvider(new RecordedListenBrainzApiClient([]));
        var result = await provider.GetCandidatesByTagsAsync(["rock"]);
        Assert.Empty(result); Assert.Contains(result.Gaps, g => g.Reason == "recording_request_not_available");
    }

    [Theory]
    [InlineData(false, true, "LiveOnly", false)]
    [InlineData(true, false, "LiveOnly", false)]
    [InlineData(true, true, "FixtureOnly", false)]
    [InlineData(true, true, "LiveOnly", true)]
    public void LiveResolutionRequiresSeparateOriginModeAndUsageGates(bool liveEnabled, bool usageApproved, string mode, bool accepted)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Generation:Provider"] = "listenbrainz", ["Generation:Origin"] = "Live", ["Generation:LiveEnabled"] = liveEnabled.ToString(),
            ["ListenBrainz:Mode"] = mode, ["ListenBrainz:LiveUseApproved"] = usageApproved.ToString()
        }).Build();
        using var services = new ServiceCollection().AddLogging().AddInfrastructure(config).BuildServiceProvider();
        using var scope = services.CreateScope();
        if (accepted)
        {
            services.GetRequiredService<GenerationConfiguration>().Scoring();
            Assert.IsType<ListenBrainzApiClient>(scope.ServiceProvider.GetRequiredService<IListenBrainzApiClient>());
        }
        else Assert.Throws<GenerationStoppedException>(() => scope.ServiceProvider.GetRequiredService<IRecommendationProviderResolver>().Resolve());
    }

    private sealed class CollectingClient : IListenBrainzApiClient
    {
        private readonly FixtureListenBrainzApiClient fixture = new();
        public List<RecordedListenBrainzResponse> Recordings { get; } = [];
        public async Task<ListenBrainzResponse> SendAsync(ListenBrainzRequest request, CancellationToken cancellationToken = default)
        {
            var result = await fixture.SendAsync(request, cancellationToken);
            byte[] body = Encoding.UTF8.GetBytes(result.Data!.Value.GetRawText());
            Recordings.Add(new(request, body, Convert.ToHexStringLower(SHA256.HashData(body)), DateTimeOffset.UnixEpoch));
            return result;
        }
    }
}
