using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
namespace LinerNotes.Application.Features.Discovery;

public sealed record DiscoveryTasteSelection(IReadOnlyList<Guid> CatalogRecordIds, IReadOnlyList<string> Tags, IReadOnlyList<Guid>? LegacySignalIds = null);
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
        if (selection.CatalogRecordIds is null || selection.Tags is null ||
            selection.CatalogRecordIds.Distinct().Count() != selection.CatalogRecordIds.Count)
            throw new ArgumentException("invalid_discovery_selection");
        var tags = selection.Tags.Select(t => ResolveTag(t ?? "")).ToArray();
        if (tags.Distinct(StringComparer.Ordinal).Count() != tags.Length)
            throw new ArgumentException("invalid_style_selection");
        var signals = new List<TasteSignal>();
        var identities = new HashSet<(CatalogKind, Guid)>();
        foreach (var id in selection.LegacySignalIds ?? [])
        {
            var legacy = retained?.SingleOrDefault(s => s.Id == id && s.ProfileId == profileId &&
                s.Source == TasteSignalSource.InitialSeedManual && s.CatalogRecordId is null && s.TargetType is TasteTargetType.Artist or TasteTargetType.Album or TasteTargetType.Track)
                ?? throw new ArgumentException("legacy_selection_unavailable");
            if (signals.Any(s => s.Id == id)) throw new ArgumentException("duplicate_legacy_selection");
            signals.Add(legacy);
        }

        var items = selection.CatalogRecordIds.Count == 0 ? new Dictionary<Guid, CatalogItemDto>()
            : (await catalog.GetManyAsync(selection.CatalogRecordIds, clock.GetUtcNow(), ct)).ToDictionary(item => item.Id);
        foreach (var id in selection.CatalogRecordIds)
        {
            items.TryGetValue(id, out var item);
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
            var signal = TasteSignal.ForProfile(profileId, null, item.Kind switch { CatalogKind.Artist => TasteTargetType.Artist, CatalogKind.Album => TasteTargetType.Album, _ => TasteTargetType.Track },
                item.Name, 1, TasteSignalSource.InitialSeedManual, "Selected from the music catalog.");
            signal.SetCatalogSelection(item.Id, item.CanonicalId, JsonSerializer.Serialize(new CatalogSelectionSnapshot("catalog-selection-v1", item)));
            signals.Add(signal);
        }
        signals.AddRange(tags.Select(t => TasteSignal.ForProfile(profileId, null, TasteTargetType.Tag, t, 1,
            TasteSignalSource.InitialSeedManual, "Selected style.")));
        return signals;

        string ResolveTag(string value)
        {
            try { return TasteVocabulary.Canonicalize(value); }
            catch (ArgumentException) when (retained?.Any(s => s.ProfileId == profileId && s.Source == TasteSignalSource.InitialSeedManual &&
                s.TargetType == TasteTargetType.Tag && s.NormalizedTargetValue == value.Trim().ToLowerInvariant()) == true)
            { return value.Trim().ToLowerInvariant(); }
        }

    }
    public async Task<long> ReplaceAsync(Guid profileId, long revision, DiscoveryTasteSelection selection, CancellationToken ct)
    {
        var signals = await ResolveAsync(profileId, selection, ct);
        return await admission.ExecuteAsync(262_144, token => store.ReplaceManualAsync(profileId, revision, signals, clock.GetUtcNow(), token), ct);
    }
}
