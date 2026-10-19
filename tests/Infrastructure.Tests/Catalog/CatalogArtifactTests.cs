using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure.Catalog;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using NSubstitute;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.Catalog;

public sealed class CatalogArtifactTests
{
    [Fact]
    public async Task ReaderRejectsOversizedLinesWithoutAllocatingTheWholeArtifact()
    {
        var bytes = Encoding.UTF8.GetBytes(new string('x', 40_000));
        using var stream = new MemoryStream(bytes);
        var manifest = Manifest(bytes);
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in new CatalogArtifactReader().ReadAsync(stream, manifest, default)) { }
        });
        Assert.InRange(stream.Position, 1, 40_000);
    }

    [Fact]
    public async Task PublicTagsRequireActualScopeAndALicense()
    {
        var row = new CatalogArtifactRow(Guid.NewGuid(), CatalogKind.Album, "Synthetic public-schema fixture", [], [Guid.NewGuid()], [new("jazz", 1)]);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(row, CatalogArtifactReader.Json);
        using var stream = new MemoryStream(bytes);
        var manifest = new CatalogPublication(Guid.NewGuid(), "MusicBrainz", "schema-only-fixture", "https://example.invalid/catalog", CatalogOrigin.Public,
            "CC0-1.0", "CC-BY-NC-SA-3.0", "Schema fixture, not acquired provider data", Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length, 1, DateTimeOffset.UnixEpoch);
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in new CatalogArtifactReader().ReadAsync(stream, manifest, default)) { }
        });
    }

    [Fact]
    public async Task ExplicitAlbumLookupResolvesAllCreditsAndDoesNotImportSearchResponseTags()
    {
        var source = new MusicBrainzCatalogLookupSource(new FixtureListenBrainzApiClient(), TimeProvider.System);
        var result = await source.SearchAsync("Any synthetic album", CatalogKind.Album, default);
        Assert.Equal(CatalogOrigin.Synthetic, result.Origin);
        var album = Assert.Single(result.Records, r => r.Kind == CatalogKind.Album);
        Assert.Single(album.ArtistIds);
        Assert.Single(result.Records, r => r.Kind == CatalogKind.Artist);
        Assert.Equal(album.Id, Assert.Single(result.MatchedCanonicalIds));
        Assert.All(result.Records, r => Assert.Empty(r.Tags));
        Assert.NotNull(result.CacheUntilUtc);
    }

    [Fact]
    public async Task MalformedCanonicalIdsAndRecordedEvaluationInputsAreRejected()
    {
        var client = Substitute.For<IListenBrainzApiClient>();
        var request = new ListenBrainzRequest(ListenBrainzRequestKind.ArtistSearch, "fixture", 10);
        var malformed = ListenBrainzResponse.Parse(request, Encoding.UTF8.GetBytes("{\"artists\":[{\"id\":42,\"name\":\"Fixture\"}]}"), EvidenceOrigin.Live, DateTimeOffset.UtcNow);
        client.SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>()).Returns(malformed);
        var source = new MusicBrainzCatalogLookupSource(client, TimeProvider.System);
        var error = await Assert.ThrowsAsync<GenerationStoppedException>(() => source.SearchAsync("fixture", CatalogKind.Artist, default));
        Assert.Equal("catalog_lookup_identity_invalid", error.Message);
        client.SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>()).Returns(malformed with
        { Reference = malformed.Reference with { Origin = EvidenceOrigin.Recorded } });
        Assert.Equal("catalog_lookup_recording_not_product_eligible",
            (await Assert.ThrowsAsync<GenerationStoppedException>(() => source.SearchAsync("fixture", CatalogKind.Artist, default))).Message);
    }

    [Fact]
    public void AlbumSearchUsesTheMusicBrainzAllowlistAndLiteralQueryEncoding()
    {
        var request = new ListenBrainzRequest(ListenBrainzRequestKind.AlbumSearch, "album: \"A/B\"", 10);
        Assert.Equal("MusicBrainz", request.Provider);
        Assert.StartsWith("ws/2/release-group/?fmt=json&limit=10&query=", request.RelativeUri());
        Assert.Contains("releasegroup:", Uri.UnescapeDataString(request.RelativeUri()));
        Assert.DoesNotContain("https://", request.RelativeUri());
    }

    [Fact]
    public async Task ReaderChecksTheBytesItConsumedBeforeSuccessfulCompletion()
    {
        var approved = new CatalogArtifactRow(Guid.NewGuid(), CatalogKind.Artist, "Approved fixture", [], [], []);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(approved, CatalogArtifactReader.Json);
        var manifest = Manifest(bytes);
        var changed = JsonSerializer.SerializeToUtf8Bytes(approved with { Name = "Changed fixture" }, CatalogArtifactReader.Json);
        using var stream = new MemoryStream(changed);
        var error = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in new CatalogArtifactReader().ReadAsync(stream, manifest, default)) { }
        });
        Assert.Equal("catalog_consumed_artifact_hash_mismatch", error.Message);
    }

    private static CatalogPublication Manifest(byte[] bytes) => new(Guid.NewGuid(), "generated-fixture", "fixture-v1", "fixture:catalog", CatalogOrigin.Synthetic,
        "synthetic-fixture", null, "Synthetic mechanics only", Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length, 1, DateTimeOffset.UnixEpoch);
}
