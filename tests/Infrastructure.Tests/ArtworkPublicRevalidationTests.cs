using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Catalog;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure.Artwork;
using LinerNotes.Infrastructure.Catalog;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using NSubstitute;
using Xunit;

namespace LinerNotes.Infrastructure.Tests;

public sealed class ArtworkPublicRevalidationTests
{
    [Fact]
    public async Task ArbitraryIdentifiersNeverReachProvider()
    {
        using var cache = new RecordingArtworkCache();
        var (service, _, metadata, _) = Create(cache);
        await service.RevalidateAsync([Guid.NewGuid()], default);
        await metadata.DidNotReceive().SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConcurrentRefreshPublishesOnlyApprovedCoreMetadataAndReusesFreshEligibility()
    {
        using var cache = new RecordingArtworkCache();
        var (service, catalog, metadata, store) = Create(cache);
        var id = Guid.NewGuid(); var other = Guid.NewGuid(); bool published = false;
        catalog.PreviouslyPublicAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()).Returns(new[] { id });
        catalog.EligibleAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Guid>>(published ? [id] : []));
        metadata.SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            Response(call.Arg<ListenBrainzRequest>(), [id, other]));
        var rows = new List<CatalogArtifactRow>();
        store.AppendAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<CatalogArtifactRow>>(), Arg.Any<CancellationToken>())
            .Returns(call => { rows.AddRange(call.Arg<IReadOnlyList<CatalogArtifactRow>>()); return Task.CompletedTask; });
        store.PublishAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ => { published = true; return Task.CompletedTask; });
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.RevalidateAsync([id, other], default)));
        await metadata.Received(1).SendAsync(Arg.Is<ListenBrainzRequest>(request => request.Identity == id.ToString("D")), Arg.Any<CancellationToken>());
        Assert.Equal(id, Assert.Single(rows, row => row.Kind == CatalogKind.Track).Id);
        Assert.All(rows, row => Assert.Empty(row.Tags));
        Assert.DoesNotContain("private-extra", JsonSerializer.Serialize(rows));
        Assert.True(published);
    }

    [Theory]
    [InlineData("no_store")]
    [InlineData("malformed")]
    [InlineData("rate_limit")]
    [InlineData("proof_removed")]
    public async Task UnsuccessfulRevalidationNeverPublishesAndAttemptsAreBounded(string failure)
    {
        using var cache = new RecordingArtworkCache();
        var (service, catalog, metadata, store) = Create(cache);
        var id = Guid.NewGuid(); int proofChecks = 0;
        catalog.PreviouslyPublicAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Guid>>(failure == "proof_removed" && ++proofChecks > 1 ? [] : [id]));
        metadata.SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<ListenBrainzRequest>();
            return failure switch
            {
                "no_store" => Response(request, [id]) with { SharedCacheUntilUtc = null },
                "malformed" => ListenBrainzResponse.Parse(request, "{}"u8.ToArray(), EvidenceOrigin.Live, DateTimeOffset.UtcNow),
                "rate_limit" => ListenBrainzResponse.Gap(request, EvidenceOrigin.Live, "rate_limit"),
                _ => Response(request, [id])
            };
        });
        if (failure == "rate_limit")
            await Assert.ThrowsAsync<DiscoverySourceException>(() => service.RevalidateAsync([id], default));
        else await service.RevalidateAsync([id], default);
        await service.RevalidateAsync([id], default);
        await metadata.Received(1).SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().StartAsync(Arg.Any<CatalogPublication>(), Arg.Any<CancellationToken>());
    }

    static ListenBrainzResponse Response(ListenBrainzRequest request, Guid[] ids)
    {
        var artist = Guid.NewGuid();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(ids.ToDictionary(id => id.ToString("D"), id => new
        {
            recording = new { name = "public recording" },
            artist = new { name = "public artist", artists = new[] { new { artist_mbid = artist, name = "public artist" } } },
            tag = new[] { new { tag = "private-extra", count = 10 } }
        }));
        return ListenBrainzResponse.Parse(request, bytes, EvidenceOrigin.Live, DateTimeOffset.UtcNow)
            with { SharedCacheUntilUtc = DateTimeOffset.UtcNow.AddMinutes(10) };
    }

    static (ArtworkPublicRevalidation, IArtworkCatalog, IListenBrainzApiClient, ICatalogImportStore) Create(RecordingArtworkCache cache)
    {
        var catalog = Substitute.For<IArtworkCatalog>();
        var metadata = Substitute.For<IListenBrainzApiClient>();
        var store = Substitute.For<ICatalogImportStore>();
        var admission = Substitute.For<IDiscoveryWriteCoordinator>();
        admission.ExecuteAsync(Arg.Any<long>(), Arg.Any<Func<CancellationToken, Task<bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<bool>>>()(call.Arg<CancellationToken>()));
        var import = new ImportCatalog(store, new CatalogArtifactReader(), admission, TimeProvider.System);
        return (new(catalog, metadata, cache, import,
            new() { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true }, TimeProvider.System), catalog, metadata, store);
    }
}
