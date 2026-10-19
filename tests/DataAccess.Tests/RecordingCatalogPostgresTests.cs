using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class CatalogPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task RecordingVersionsCreditsAndReleaseHintsSurviveCatalogEvictionInFavoriteSnapshot()
    {
        await using var scope = _services.CreateAsyncScope();
        var artist = Guid.NewGuid(); var recording = Guid.NewGuid(); var otherVersion = Guid.NewGuid(); var release = Guid.NewGuid();
        CatalogArtifactRow[] rows = [new(artist, CatalogKind.Artist, "Synthetic credited artist", [], [], []),
            new(recording, CatalogKind.Track, "Synthetic version", [], [artist], [], "Live recording", null,
                [new(release, "Synthetic live release", "2000-01-01")]),
            new(otherVersion, CatalogKind.Track, "Synthetic version", [], [artist], [], "Studio recording")];
        await ImportAsync(scope.ServiceProvider, rows);
        var reader = scope.ServiceProvider.GetRequiredService<ICatalogReader>();
        var results = await reader.SearchAsync("Synthetic version", CatalogKind.Track, 10, 0, DateTimeOffset.UtcNow, default);
        Assert.Equal(2, results.Items.Count);
        var item = Assert.Single(results.Items.Where(i => i.CanonicalId == recording));
        Assert.Equal(artist, Assert.Single(item.Artists!).CanonicalId);
        Assert.Equal(release, Assert.Single(item.Releases!).CanonicalId);
        var select = scope.ServiceProvider.GetRequiredService<SelectDiscoveryTaste>();
        var profileId = Guid.NewGuid();
        var selected = await select.ResolveAsync(profileId, new([item.Id], []), default);
        await scope.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(0, async ct =>
        {
            await scope.ServiceProvider.GetRequiredService<DataContext>().CatalogRecords.Where(r => r.Id == item.Id).ExecuteDeleteAsync(ct);
            return true;
        }, default);
        var retained = Assert.Single(await select.ResolveAsync(profileId, new([item.Id], []), default, selected));
        var snapshot = JsonSerializer.Deserialize<CatalogSelectionSnapshot>(retained.SelectionJson!)!;
        Assert.Equal(recording, snapshot.Item.CanonicalId); Assert.Equal("Live recording", snapshot.Item.Disambiguation);
        Assert.Equal("Synthetic live release", Assert.Single(snapshot.Item.Releases!).Title);
        await Assert.ThrowsAsync<ArgumentException>(() => select.ResolveAsync(Guid.NewGuid(), new([item.Id], []), default, selected));
    }
}
