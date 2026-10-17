using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
namespace LinerNotes.Application.Features.Discovery;

public sealed class DiscoveryTasteOptions
{
    public double AlbumArtistFallbackInfluence { get; set; } = 0.35;
    public void Validate()
    {
        if (!double.IsFinite(AlbumArtistFallbackInfluence) || AlbumArtistFallbackInfluence is < 0 or > 1)
            throw new ArgumentException("invalid_album_fallback_influence");
    }
}
public sealed record CanonicalArtistSeed(Guid Id, string Name);
public sealed record MaterializedDiscoveryTaste(WeightedTagVector Vector, IReadOnlyList<StoredSeed> Seeds,
    IReadOnlyList<CanonicalArtistSeed> Artists, IReadOnlyList<string> Tags, IReadOnlyList<CoverageGap> Gaps,
    IReadOnlySet<string> FamiliarArtists, DateTimeOffset? EvidenceExpiresAtUtc = null);

public sealed class MaterializeDiscoveryTaste(IRecommendationProviderResolver resolver, GenerationConfiguration configuration,
    DiscoveryTasteOptions options, TimeProvider clock, IDiscoverySeedPreparation? preparation = null)
{
    public Task<MaterializedDiscoveryTaste> BuildAsync(IReadOnlyList<TasteSignal> signals, CancellationToken ct)
        => BuildAsync(signals, true, ct);
    public async Task<MaterializedDiscoveryTaste> BuildAsync(IReadOnlyList<TasteSignal> signals, bool allowAcquisition, CancellationToken ct)
    {
        options.Validate();
        var provider = resolver.Resolve();
        var evidence = new List<EntityTasteEvidence>();
        var stored = new List<StoredSeed>();
        var artists = new Dictionary<Guid, CanonicalArtistSeed>();
        var familiar = new HashSet<string>(StringComparer.Ordinal);
        var gaps = new List<CoverageGap>();
        var expiries = new List<DateTimeOffset>();
        var positives = signals.Where(s => s.Weight > 0).ToArray();
        if (positives.Length > 200) throw new GenerationStoppedException("taste_evidence_limit");
        foreach (var signal in positives.Where(s => s.TargetType is TasteTargetType.Artist or TasteTargetType.Album).OrderBy(s => s.Source != TasteSignalSource.InitialSeedManual))
        {
            ct.ThrowIfCancellationRequested();
            if (signal.SelectionJson is null || signal.CanonicalEntityId is null)
            {
                // Preserve legacy name seeds without pretending they identify a canonical entity.
                var legacy = await Seed(new(null, signal.TargetValue, false));
                Add(signal, legacy, "legacy-artist:" + signal.NormalizedTargetValue, "artist", 1, null);
                familiar.Add(signal.NormalizedTargetValue);
                continue;
            }
            var selected = JsonSerializer.Deserialize<CatalogSelectionSnapshot>(signal.SelectionJson)
                ?? throw new GenerationStoppedException("invalid_selection_evidence");
            var item = selected.Item;
            if (selected.Version != "catalog-selection-v1" || item.CanonicalId != signal.CanonicalEntityId || item.Id != signal.CatalogRecordId ||
                item.Kind != (signal.TargetType == TasteTargetType.Album ? CatalogKind.Album : CatalogKind.Artist))
                throw new GenerationStoppedException("selection_identity_mismatch");
            var credits = item.Kind == CatalogKind.Artist ? new[] { new CatalogArtistCreditDto(item.CanonicalId, item.Name, 0) } :
                item.Artists?.ToArray() ?? [];
            foreach (var credit in credits)
            {
                artists.TryAdd(credit.CanonicalId, new(credit.CanonicalId, credit.Name));
                familiar.Add(credit.Name.Trim().ToLowerInvariant());
            }
            string scope = item.Kind == CatalogKind.Artist ? "artist" : "release-group";
            string identity = scope + ":" + item.CanonicalId.ToString("D");
            var local = item.ExpiresAtUtc is null || item.ExpiresAtUtc > clock.GetUtcNow()
                ? (item.TagEvidence ?? []).Where(t => t.Scope == scope || t.Scope == "synthetic-" + scope).ToArray() : [];
            SeedTagSnapshot snapshot;
            if (local.Length > 0)
            {
                if (item.ExpiresAtUtc is { } localExpiry) expiries.Add(localExpiry);
                var origin = item.Origin == CatalogOrigin.Synthetic ? EvidenceOrigin.Synthetic : EvidenceOrigin.Live;
                var response = new ResponseReference(item.Source, "catalog.tags", identity + ":" + item.Version.ToString("D"), origin,
                    item.ContentSha256, item.AcquiredAtUtc);
                snapshot = new(item.Name, WeightedTagVector.FromDictionary(local.GroupBy(t => t.Name.Trim().ToLowerInvariant(), StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Max(t => t.Weight), StringComparer.Ordinal)), [], response, null, []);
            }
            else if (preparation is not null || provider.SeedTags is ICanonicalSeedTagSource)
                snapshot = await Seed(new(item.CanonicalId, item.Name, item.Kind == CatalogKind.Album));
            else snapshot = new(item.Name, WeightedTagVector.Empty, [],
                new(provider.Id, "seed.tags", identity, configuration.Origin, null, null), "canonical_seed_tags_not_supported", []);
            Add(signal, snapshot, identity, scope, 1, null);
            if (item.Kind == CatalogKind.Album && snapshot.Vector.Weights.Count == 0)
            {
                gaps.Add(new(identity, "album.tags", "album_tags_missing_using_credited_artists", snapshot.Response));
                foreach (var credit in credits)
                {
                    if (preparation is null && provider.SeedTags is not ICanonicalSeedTagSource) continue;
                    var fallback = await Seed(new(credit.CanonicalId, credit.Name, false));
                    Add(signal, fallback, "artist:" + credit.CanonicalId.ToString("D"), "artist", options.AlbumArtistFallbackInfluence,
                        "album_tags_missing_using_credited_artist");
                }
            }
        }
        return new(DiscoveryTasteMaterializer.Build(positives, evidence), stored,
            artists.Values.Take(configuration.MaximumArtistSeeds).OrderBy(a => a.Id).ToArray(),
            positives.Where(s => s.TargetType == TasteTargetType.Tag).Select(s => s.NormalizedTargetValue).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).Take(configuration.MaximumTagSeeds).ToArray(), gaps.Distinct().ToArray(), familiar, expiries.Count == 0 ? null : expiries.Min());

        async Task<SeedTagSnapshot> Seed(DiscoverySeedRequest request)
        {
            if (preparation is not null) return await preparation.GetAsync(request, allowAcquisition, ct);
            if (!allowAcquisition) throw new LinerNotes.Application.Common.Interfaces.Discovery.DiscoveryPreparationRequiredException();
            if (request.EntityId is { } id && provider.SeedTags is ICanonicalSeedTagSource canonical)
                return request.IsAlbum ? await canonical.GetAlbumAsync(id, request.Name, ct) : await canonical.GetArtistAsync(id, request.Name, ct);
            return await provider.SeedTags.GetAsync(request.Name, ct);
        }
        void Add(TasteSignal signal, SeedTagSnapshot snapshot, string identity, string scope, double influence, string? missing)
        {
            if (snapshot.SharedUntilUtc is { } expiry) expiries.Add(expiry);
            if (snapshot.Response.Origin != configuration.Origin || snapshot.Response.Origin == EvidenceOrigin.Recorded ||
                snapshot.Tags.Any(t => t.Response.Origin != configuration.Origin) || snapshot.SupportingResponses.Any(r => r.Origin != configuration.Origin))
                throw new GenerationStoppedException("seed_evidence_origin_mismatch");
            evidence.Add(new(signal.Id, identity, scope, snapshot.Vector, influence, missing ?? snapshot.MissingReason));
            stored.Add(snapshot.Store() with { SignalId = signal.Id, EntityIdentity = identity, TagScope = scope,
                Influence = influence, MissingReason = missing ?? snapshot.MissingReason });
            gaps.AddRange(snapshot.Gaps);
        }
    }
}
