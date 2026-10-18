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
    public async Task TagOnlySelectionIsAllowedButOversizedOrUnknownStylesAreRejected()
    {
        var catalog = Substitute.For<ICatalogReader>();
        var select = new SelectDiscoveryTaste(catalog, Substitute.For<IDiscoveryTasteStore>(), Substitute.For<IDiscoveryWriteCoordinator>(), TimeProvider.System);
        var result = await select.ResolveAsync(Guid.NewGuid(), new([], ["jazz"]), default);
        Assert.Equal(TasteTargetType.Tag, Assert.Single(result).TargetType);
        await Assert.ThrowsAsync<ArgumentException>(() => select.ResolveAsync(Guid.NewGuid(), new([], ["unknown"]), default));
        await Assert.ThrowsAsync<ArgumentException>(() => select.ResolveAsync(Guid.NewGuid(), new([], []), default));
        await Assert.ThrowsAsync<ArgumentException>(() => select.ResolveAsync(Guid.NewGuid(), new(Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray(), []), default));
        await catalog.DidNotReceiveWithAnyArgs().GetAsync(default, default, default);
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
