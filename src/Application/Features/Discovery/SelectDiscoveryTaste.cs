using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
namespace LinerNotes.Application.Features.Discovery;

public sealed record DiscoveryTasteSelection(IReadOnlyList<Guid> CatalogRecordIds, IReadOnlyList<string> Tags);
public sealed record CatalogSelectionSnapshot(string Version, CatalogItemDto Item);
public sealed class SelectDiscoveryTaste(ICatalogReader catalog, IDiscoveryTasteStore store,
    IDiscoveryWriteCoordinator admission, TimeProvider clock)
{
    public static readonly IReadOnlyList<string> Styles = Array.AsReadOnly(new[] {
        "indie", "electronic", "jazz", "rock", "pop", "hip hop", "classical", "folk", "ambient", "experimental",
        "metal", "punk", "soul", "r&b", "funk", "disco", "house", "techno", "drum and bass", "trip hop",
        "shoegaze", "dream pop", "post punk", "post rock", "alternative", "synthpop", "psychedelic", "blues",
        "country", "reggae", "dub", "afrobeat", "latin", "soundtrack", "downtempo", "acoustic", "noise", "world"
    });
    // Resolve reads outside admission. No provider I/O occurs here.
    public async Task<IReadOnlyList<TasteSignal>> ResolveAsync(Guid profileId, DiscoveryTasteSelection selection, CancellationToken ct, IReadOnlyList<TasteSignal>? retained = null)
    {
        if (selection.CatalogRecordIds is null || selection.Tags is null || selection.CatalogRecordIds.Count > 5 ||
            selection.Tags.Count > 5 || selection.CatalogRecordIds.Count + selection.Tags.Count == 0 ||
            selection.CatalogRecordIds.Distinct().Count() != selection.CatalogRecordIds.Count)
            throw new ArgumentException("select_one_to_five_artists_or_albums_and_up_to_five_styles");
        var tags = selection.Tags.Select(t => (t ?? "").Trim().ToLowerInvariant()).ToArray();
        if (tags.Distinct(StringComparer.Ordinal).Count() != tags.Length || tags.Any(t => !Styles.Contains(t, StringComparer.Ordinal)))
            throw new ArgumentException("invalid_style_selection");
        var signals = new List<TasteSignal>();
        var identities = new HashSet<(CatalogKind, Guid)>();
        foreach (var id in selection.CatalogRecordIds)
        {
            var item = await catalog.GetAsync(id, clock.GetUtcNow(), ct);
            if (item is null && retained is not null)
            {
                var prior = retained.FirstOrDefault(s => s.ProfileId == profileId && s.Source == TasteSignalSource.InitialSeedManual &&
                    s.CatalogRecordId == id && s.SelectionJson is not null);
                if (prior is not null)
                {
                    var snapshot = JsonSerializer.Deserialize<CatalogSelectionSnapshot>(prior.SelectionJson!);
                    if (snapshot?.Version == "catalog-selection-v1" && snapshot.Item.Id == id && snapshot.Item.CanonicalId == prior.CanonicalEntityId)
                        item = snapshot.Item;
                }
            }
            if (item is null) throw new ArgumentException("catalog_selection_unavailable");
            if (!identities.Add((item.Kind, item.CanonicalId))) throw new ArgumentException("duplicate_catalog_selection");
            var signal = TasteSignal.ForProfile(profileId, null, item.Kind == CatalogKind.Artist ? TasteTargetType.Artist : TasteTargetType.Album,
                item.Name, 1, TasteSignalSource.InitialSeedManual, "Selected from the music catalog.");
            signal.SetCatalogSelection(item.Id, item.CanonicalId, JsonSerializer.Serialize(new CatalogSelectionSnapshot("catalog-selection-v1", item)));
            signals.Add(signal);
        }
        signals.AddRange(tags.Select(t => TasteSignal.ForProfile(profileId, null, TasteTargetType.Tag, t, 1,
            TasteSignalSource.InitialSeedManual, "Selected style.")));
        return signals;
    }
    public async Task<long> ReplaceAsync(Guid profileId, long revision, DiscoveryTasteSelection selection, CancellationToken ct)
    {
        var signals = await ResolveAsync(profileId, selection, ct);
        return await admission.ExecuteAsync(262_144, token => store.ReplaceManualAsync(profileId, revision, signals, clock.GetUtcNow(), token), ct);
    }
}
