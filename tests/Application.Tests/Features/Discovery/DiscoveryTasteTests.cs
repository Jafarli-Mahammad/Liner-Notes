using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
using NSubstitute;
using Xunit;
namespace LinerNotes.Application.Tests.Features.Discovery;

public sealed class DiscoveryTasteTests
{
    [Fact]
    public async Task PreviouslyQueuedV1RequestKeysReplayAfterOnboardingUpgrade()
    {
        var profile = LinerNotes.Domain.Discovery.ListenerProfile.Guest(DateTimeOffset.UtcNow, TimeSpan.FromDays(7));
        var selection = new DiscoveryTasteSelection([], ["jazz"]);
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version = "discovery-intent-v1",
            Revision = 0L,
            Records = Array.Empty<Guid>(),
            Tags = new[] { "jazz" }
        })));
        var prior = new LinerNotes.Domain.Discovery.DiscoveryJob(profile.Id, "v1-retry", hash,
            LinerNotes.Domain.Discovery.DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow);
        var batches = Substitute.For<IDiscoveryBatchStore>();
        batches.ReadInputsAsync(profile.Id, Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new DiscoveryInputs(profile, [], []));
        var jobs = Substitute.For<IDiscoveryJobStore>();
        jobs.FindByKeyAsync(profile.Id, "v1-retry", Arg.Any<CancellationToken>()).Returns(prior);
        var taste = Substitute.For<IDiscoveryTasteStore>();
        var admission = Substitute.For<IDiscoveryWriteCoordinator>();
        var handler = new SubmitDiscoveryHandler(batches, jobs, taste,
            new(Substitute.For<ICatalogReader>(), taste, admission, TimeProvider.System), admission, new() { RunJobs = true }, TimeProvider.System);
        Assert.Same(prior, await handler.Handle(new(profile.Id, 0, "v1-retry", selection), default));
        await taste.DidNotReceiveWithAnyArgs().ReplaceManualAsync(default, default, default!, default, default);
    }

    [Fact]
    public async Task ExistingNameSeedsAndUncataloguedTagsRemainEditableOnlyByTheirOwner()
    {
        var profile = Guid.NewGuid();
        var artist = TasteSignal.ForProfile(profile, null, TasteTargetType.Artist, "Existing name seed", 1, TasteSignalSource.InitialSeedManual, "Prior setup");
        var tag = TasteSignal.ForProfile(profile, null, TasteTargetType.Tag, "existing custom tag", 1, TasteSignalSource.InitialSeedManual, "Prior setup");
        var select = new SelectDiscoveryTaste(Substitute.For<ICatalogReader>(), Substitute.For<IDiscoveryTasteStore>(), Substitute.For<IDiscoveryWriteCoordinator>(), TimeProvider.System);
        var selection = new DiscoveryTasteSelection([], [tag.TargetValue], [artist.Id]);
        var result = await select.ResolveAsync(profile, selection, default, [artist, tag]);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, signal => signal.Id == artist.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => select.ResolveAsync(Guid.NewGuid(), selection, default, [artist, tag]));
        await Assert.ThrowsAsync<ArgumentException>(() => select.ResolveAsync(profile, new([], ["new arbitrary tag"]), default, [artist, tag]));
    }

    [Fact]
    public async Task RetainedFavoriteCanBeReselectedAfterCatalogEvictionOnlyForItsOwner()
    {
        var catalog = Substitute.For<ICatalogReader>();
        var prior = Album(Guid.NewGuid(), Guid.NewGuid(), [], DateTimeOffset.UnixEpoch);
        var select = new SelectDiscoveryTaste(catalog, Substitute.For<IDiscoveryTasteStore>(), Substitute.For<IDiscoveryWriteCoordinator>(), TimeProvider.System);
        var result = await select.ResolveAsync(prior.ProfileId, new([prior.CatalogRecordId!.Value], []), default, [prior]);
        Assert.Equal(prior.CanonicalEntityId, Assert.Single(result).CanonicalEntityId);
        await Assert.ThrowsAsync<ArgumentException>(() => select.ResolveAsync(Guid.NewGuid(), new([prior.CatalogRecordId.Value], []), default, [prior]));
    }
    [Fact]
    public async Task PreparedOnlyMaterializationUsesCatalogTagsAndNeverFallsThroughToProvider()
    {
        var source = Substitute.For<ISeedTagSource, ICanonicalSeedTagSource>();
        var builder = Builder(source);
        var local = await builder.BuildAsync([Album(Guid.NewGuid(), Guid.NewGuid(), [new("ambient", 1, "synthetic-release-group")])], false, default);
        Assert.Equal(1, local.Vector.Weights["ambient"]);
        await Assert.ThrowsAsync<DiscoveryPreparationRequiredException>(() => builder.BuildAsync(
            [Album(Guid.NewGuid(), Guid.NewGuid(), [])], false, default));
        await ((ICanonicalSeedTagSource)source).DidNotReceiveWithAnyArgs().GetAlbumAsync(default, default!, default);
        await source.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
    }

    [Fact]
    public async Task TagAndEmptySelectionsAreAllowedButUnknownTagsAreRejected()
    {
        var catalog = Substitute.For<ICatalogReader>();
        var select = new SelectDiscoveryTaste(catalog, Substitute.For<IDiscoveryTasteStore>(), Substitute.For<IDiscoveryWriteCoordinator>(), TimeProvider.System);
        var result = await select.ResolveAsync(Guid.NewGuid(), new([], ["jazz"]), default);
        Assert.Equal(TasteTargetType.Tag, Assert.Single(result).TargetType);
        await Assert.ThrowsAsync<ArgumentException>(() => select.ResolveAsync(Guid.NewGuid(), new([], ["unknown"]), default));
        Assert.Empty(await select.ResolveAsync(Guid.NewGuid(), new([], []), default));
        var allTags = TasteVocabulary.Entries.Select(t => t.Id).ToArray();
        Assert.True(allTags.Length > 200);
        Assert.Equal(allTags.Length, (await select.ResolveAsync(Guid.NewGuid(), new([], allTags), default)).Count);
        await catalog.DidNotReceiveWithAnyArgs().GetAsync(default, default, default);
        await catalog.DidNotReceiveWithAnyArgs().GetManyAsync(default!, default, default);
    }
    [Fact]
    public async Task AlbumTagsHaveTheirOwnScopeAndDoNotCauseArtistFallback()
    {
        var source = Substitute.For<ISeedTagSource, ICanonicalSeedTagSource>();
        var canonical = (ICanonicalSeedTagSource)source;
        Guid album = Guid.NewGuid(), artist = Guid.NewGuid();
        var signal = Album(album, artist, [new("ambient", 1, "synthetic-release-group")]);
        var result = await Builder(source).BuildAsync([signal], default);
        Assert.Equal(1, result.Vector["ambient"]);
        Assert.Equal("release-group", Assert.Single(result.Seeds).TagScope);
        Assert.Equal(artist, Assert.Single(result.Artists).Id);
        await canonical.DidNotReceiveWithAnyArgs().GetArtistAsync(default, default!, default);
        await canonical.DidNotReceiveWithAnyArgs().GetAlbumAsync(default, default!, default);
    }
    [Fact]
    public async Task MissingAlbumTagsFallBackToCreditedArtistsWithoutRepeatedInfluence()
    {
        var source = Substitute.For<ISeedTagSource, ICanonicalSeedTagSource>();
        var canonical = (ICanonicalSeedTagSource)source;
        canonical.GetAlbumAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Snapshot("Album", false));
        canonical.GetArtistAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Snapshot("Artist", true));
        Guid artist = Guid.NewGuid();
        var result = await Builder(source).BuildAsync([Album(Guid.NewGuid(), artist, []), Album(Guid.NewGuid(), artist, [])], default);
        Assert.Equal(.35, result.Vector["jazz"]);
        Assert.Single(result.Artists);
        Assert.All(result.Seeds.Where(s => s.Influence < 1), s => { Assert.Equal("artist", s.TagScope); Assert.Contains("album_tags_missing", s.MissingReason); });
        Assert.Contains(result.Gaps, g => g.Reason == "album_tags_missing_using_credited_artists");
    }
    [Fact]
    public async Task ExpiredAlbumTagsAreReacquiredAndEvaluationEvidenceIsRejected()
    {
        var source = Substitute.For<ISeedTagSource, ICanonicalSeedTagSource>();
        var canonical = (ICanonicalSeedTagSource)source;
        canonical.GetAlbumAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Snapshot("Album", true));
        var signal = Album(Guid.NewGuid(), Guid.NewGuid(), [new("expired", 1, "synthetic-release-group")], DateTimeOffset.UnixEpoch);
        var result = await Builder(source).BuildAsync([signal], default);
        Assert.Equal(0, result.Vector["expired"]);
        Assert.Equal(1, result.Vector["jazz"]);
        canonical.GetAlbumAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
            Snapshot("Album", true) with { Response = new("ListenBrainz", "metadata.release_group", "id", EvidenceOrigin.Recorded, new string('a', 64), DateTimeOffset.UnixEpoch) });
        await Assert.ThrowsAsync<GenerationStoppedException>(() => Builder(source).BuildAsync([signal], default));
    }
    private static MaterializeDiscoveryTaste Builder(ISeedTagSource source)
    {
        var resolver = Substitute.For<IRecommendationProviderResolver>();
        resolver.Resolve().Returns(new RecommendationProvider("listenbrainz", null!, null!, source));
        return new(resolver, new GenerationConfiguration { Origin = EvidenceOrigin.Synthetic }, new(), TimeProvider.System);
    }
    private static SeedTagSnapshot Snapshot(string name, bool tags) => new(name, tags ? WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["jazz"] = 1 }) : WeightedTagVector.Empty,
        [], new("ListenBrainz", "metadata", "id", EvidenceOrigin.Synthetic, new string('a', 64), DateTimeOffset.UnixEpoch), tags ? null : "tags_missing", []);
    private static TasteSignal Album(Guid album, Guid artist, CatalogTagInput[] tags, DateTimeOffset? expiry = null)
    {
        var signal = TasteSignal.ForProfile(Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"), null, TasteTargetType.Album, "Album", 1, TasteSignalSource.InitialSeedManual, "Selected.");
        var item = new CatalogItemDto(Guid.NewGuid(), album, CatalogKind.Album, "Album", "Artist", null, null, tags.Select(t => t.Name).ToArray(),
            "generated-fixture", "Synthetic fixture", Guid.NewGuid(), CatalogOrigin.Synthetic, [new(artist, "Artist", 0)], "synthetic-fixture", "synthetic-fixture", tags,
            DateTimeOffset.UnixEpoch, expiry, new string('a', 64));
        signal.SetCatalogSelection(item.Id, album, JsonSerializer.Serialize(new CatalogSelectionSnapshot("catalog-selection-v1", item)));
        return signal;
    }
}
