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
        if (!double.IsFinite(AlbumArtistFallbackInfluence) || AlbumArtistFallbackInfluence is <= 0 or >= 1)
            throw new ArgumentException("invalid_album_fallback_influence");
    }
}
public sealed record CanonicalArtistSeed(Guid Id, string Name);
public sealed record MaterializedDiscoveryTaste(WeightedTagVector Vector, IReadOnlyList<StoredSeed> Seeds,
    IReadOnlyList<CanonicalArtistSeed> Artists, IReadOnlyList<string> Tags, IReadOnlyList<CoverageGap> Gaps,
    IReadOnlySet<string> FamiliarArtists, DateTimeOffset? EvidenceExpiresAtUtc = null)
{
    public IReadOnlyList<CanonicalArtistSeed> Recordings { get; init; } = [];
    public IReadOnlySet<Guid> FamiliarArtistIds { get; init; } = new HashSet<Guid>();
    public IReadOnlyList<string> LegacyArtists { get; init; } = [];
    public IReadOnlyList<StoredAlbumFallback> AlbumFallbacks { get; init; } = [];
    public IReadOnlyList<FavoriteEvidenceState> FavoriteEvidence { get; init; } = [];
}

public sealed class MaterializeDiscoveryTaste(IRecommendationProviderResolver resolver, GenerationConfiguration configuration,
    DiscoveryTasteOptions options, TimeProvider clock, IDiscoverySeedPreparation? preparation = null)
{
    public const string Version = "cached-favorites-v1";
    public Task<MaterializedDiscoveryTaste> BuildAsync(IReadOnlyList<TasteSignal> signals, CancellationToken ct)
        => BuildAsync(signals, true, ct);
    public async Task<MaterializedDiscoveryTaste> BuildAsync(IReadOnlyList<TasteSignal> signals, bool allowAcquisition, CancellationToken ct, long round = 0,
        IReadOnlyDictionary<Guid, Guid>? feedbackRecordings = null)
    {
        using var measurement = LinerNotes.Application.Common.Diagnostics.DiscoveryDiagnostics.Measure("preparation.taste");
        options.Validate();
        var provider = resolver.Resolve();
        var evidence = new List<EntityTasteEvidence>();
        var stored = new List<StoredSeed>();
        var artists = new Dictionary<Guid, CanonicalArtistSeed>();
        var legacyArtists = new Dictionary<string, string>(StringComparer.Ordinal);
        var recordings = new List<CanonicalArtistSeed>();
        var albumFallbacks = new List<StoredAlbumFallback>();
        var familiar = new HashSet<string>(StringComparer.Ordinal);
        var familiarIds = new HashSet<Guid>();
        var gaps = new List<CoverageGap>();
        var expiries = new List<DateTimeOffset>();
        var positives = signals.Where(s => s.Weight > 0).ToArray();

        var entities = positives.Where(s => s.TargetType is TasteTargetType.Artist or TasteTargetType.Album or TasteTargetType.Track)
            .OrderBy(s => s.Source != TasteSignalSource.InitialSeedManual)
            .ThenBy(s => s.NormalizedTargetValue, StringComparer.Ordinal).ThenBy(s => s.Id).ToArray();
        string Key(TasteSignal s)
        {
            string? hint = s.Source == TasteSignalSource.RecommendationLike && feedbackRecordings?.TryGetValue(s.Id, out var recordingId) == true
                ? ":" + recordingId.ToString("D") : null;
            return s.TargetType + ":" + (s.CanonicalEntityId?.ToString("D") ?? s.NormalizedTargetValue + hint);
        }
        var acquisition = LinerNotes.Domain.Discovery.DiscoverySelectionPolicy.Window(entities.Select(Key).Distinct(StringComparer.Ordinal).ToArray(),
            configuration.MaximumArtistSeeds, round).ToHashSet(StringComparer.Ordinal);
        var seedReads = new Dictionary<string, SeedTagSnapshot>(StringComparer.Ordinal);
        var coverage = new Dictionary<Guid, FavoriteEvidenceState>();
        var missingAlbums = new List<(TasteSignal Signal, string Identity, CatalogArtistCreditDto[] Credits, bool DirectAvailable)>();
        foreach (var signal in entities)
        {
            ct.ThrowIfCancellationRequested();
            bool scheduled = acquisition.Contains(Key(signal));
            if (signal.SelectionJson is null || signal.CanonicalEntityId is null)
            {
                if (signal.TargetType == TasteTargetType.Track)
                {
                    gaps.Add(new(signal.TargetValue, "recording.similar", "direct_recording_identity_missing"));
                    coverage[signal.Id] = new(signal.Id, Key(signal), "unavailable", "direct_recording_identity_missing");
                    continue;
                }
                familiar.Add(signal.NormalizedTargetValue);
                Guid? recording = signal.Source == TasteSignalSource.RecommendationLike && feedbackRecordings?.TryGetValue(signal.Id, out var id) == true ? id : null;
                var legacy = await Seed(new(null, signal.TargetValue, false, ArtistRecordingId: recording), scheduled);
                if (legacy?.CanonicalArtistId is { } artistId)
                {
                    artists.TryAdd(artistId, new(artistId, signal.TargetValue));
                    familiarIds.Add(artistId);
                }
                else legacyArtists.TryAdd(signal.NormalizedTargetValue, signal.TargetValue);
                Record(signal, legacy, legacy?.CanonicalArtistId is { } canonicalId ? "artist:" + canonicalId.ToString("D")
                    : "legacy-artist:" + signal.NormalizedTargetValue, "artist");
                continue;
            }
            var selected = JsonSerializer.Deserialize<CatalogSelectionSnapshot>(signal.SelectionJson)
                ?? throw new GenerationStoppedException("invalid_selection_evidence");
            var item = selected.Item;
            if (selected.Version != "catalog-selection-v1" || item.CanonicalId != signal.CanonicalEntityId || item.Id != signal.CatalogRecordId ||
                item.Kind != (signal.TargetType switch { TasteTargetType.Album => CatalogKind.Album, TasteTargetType.Track => CatalogKind.Track, _ => CatalogKind.Artist }))
                throw new GenerationStoppedException("selection_identity_mismatch");
            var credits = item.Kind == CatalogKind.Artist ? new[] { new CatalogArtistCreditDto(item.CanonicalId, item.Name, 0) } : item.Artists?.ToArray() ?? [];
            foreach (var credit in credits)
            {
                familiarIds.Add(credit.CanonicalId);
                if (item.Kind != CatalogKind.Track) artists.TryAdd(credit.CanonicalId, new(credit.CanonicalId, credit.Name));
                if (item.Kind == CatalogKind.Album) albumFallbacks.Add(new(item.CanonicalId, item.Name, credit.CanonicalId, credit.Name,
                    options.AlbumArtistFallbackInfluence / Math.Max(1, credits.Length)));
                familiar.Add(credit.Name.Trim().ToLowerInvariant());
            }
            if (item.Kind == CatalogKind.Track) recordings.Add(new(item.CanonicalId, item.Name));
            string scope = item.Kind switch { CatalogKind.Artist => "artist", CatalogKind.Album => "release-group", _ => "recording" };
            string identity = scope + ":" + item.CanonicalId.ToString("D");
            var local = item.ExpiresAtUtc is null || item.ExpiresAtUtc > clock.GetUtcNow()
                ? (item.TagEvidence ?? []).Where(t => t.Scope == scope || t.Scope == "synthetic-" + scope).ToArray() : [];
            SeedTagSnapshot? snapshot;
            if (local.Length > 0)
            {
                if (item.ExpiresAtUtc is { } localExpiry) expiries.Add(localExpiry);
                var origin = item.Origin == CatalogOrigin.Synthetic ? EvidenceOrigin.Synthetic : EvidenceOrigin.Live;
                var response = new ResponseReference(item.Source, "catalog.tags", identity + ":" + item.Version.ToString("D"), origin,
                    item.ContentSha256, item.AcquiredAtUtc);
                snapshot = new(item.Name, WeightedTagVector.FromDictionary(local.GroupBy(t => t.Name.Trim().ToLowerInvariant(), StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Max(t => t.Weight), StringComparer.Ordinal)), [], response, null, []);
            }
            else if (preparation is not null || provider.SeedTags is ICanonicalSeedTagSource || provider.SeedTags is IRecordingSeedTagSource)
                snapshot = await Seed(new(item.CanonicalId, item.Name, item.Kind == CatalogKind.Album, item.Kind == CatalogKind.Track), scheduled);
            else snapshot = new(item.Name, WeightedTagVector.Empty, [],
                new(provider.Id, "seed.tags", identity, configuration.Origin, null, null), "canonical_seed_tags_not_supported", []);
            Record(signal, snapshot, identity, scope);
            if (item.Kind == CatalogKind.Album && (snapshot is null || snapshot.Vector.IsEmpty))
                missingAlbums.Add((signal, identity, credits, snapshot is not null));
        }
        var fallbackWindow = LinerNotes.Domain.Discovery.DiscoverySelectionPolicy.Window(missingAlbums.Where(a => a.DirectAvailable)
            .SelectMany(a => a.Credits).Select(c => c.CanonicalId).Distinct().Order().ToArray(), configuration.MaximumArtistSeeds, round).ToHashSet();
        foreach (var album in missingAlbums)
        {
            gaps.Add(new(album.Identity, "album.tags", album.DirectAvailable ? "album_tags_missing_using_credited_artists" : "album_evidence_not_prepared"));
            string fallbackReason = album.DirectAvailable ? "album_tags_missing_using_credited_artist" : "album_evidence_awaiting_using_cached_credited_artist";
            bool supported = false, pending = false;
            foreach (var credit in album.Credits)
            {
                if (preparation is null && provider.SeedTags is not ICanonicalSeedTagSource) continue;
                var fallback = await Seed(new(credit.CanonicalId, credit.Name, false), album.DirectAvailable && fallbackWindow.Contains(credit.CanonicalId));
                if (fallback is null) { pending = true; continue; }
                Add(album.Signal, fallback, "artist:" + credit.CanonicalId.ToString("D"), "artist",
                    options.AlbumArtistFallbackInfluence, fallbackReason);
                supported |= !fallback.Vector.IsEmpty;
            }
            if (supported) coverage[album.Signal.Id] = new(album.Signal.Id, album.Identity, "artist-fallback", fallbackReason);
            else if (pending) coverage[album.Signal.Id] = new(album.Signal.Id, album.Identity, "awaiting", "credited_artist_evidence_not_prepared");
        }
        return new(DiscoveryTasteMaterializer.Build(positives, evidence), stored,
            artists.Values.OrderBy(a => a.Id).ToArray(),
            LinerNotes.Domain.Discovery.DiscoverySelectionPolicy.Window(positives.Where(s => s.TargetType == TasteTargetType.Tag).Select(s => s.NormalizedTargetValue).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray(), configuration.MaximumTagSeeds, round), gaps.Distinct().ToArray(), familiar, expiries.Count == 0 ? null : expiries.Min())
        { FamiliarArtistIds = familiarIds, LegacyArtists = legacyArtists.Values.Order(StringComparer.Ordinal).ToArray(), Recordings = recordings.DistinctBy(r => r.Id).ToArray(), AlbumFallbacks = albumFallbacks.Distinct().ToArray(), FavoriteEvidence = coverage.Values.ToArray() };

        void Record(TasteSignal signal, SeedTagSnapshot? snapshot, string identity, string scope)
        {
            if (snapshot is null)
            {
                coverage[signal.Id] = new(signal.Id, identity, "awaiting", "favorite_evidence_not_prepared");
                gaps.Add(new(identity, "seed.tags", "favorite_evidence_not_prepared"));
                return;
            }
            Add(signal, snapshot, identity, scope, 1, null);
            coverage[signal.Id] = new(signal.Id, identity, snapshot.Vector.IsEmpty ? "unavailable" : "supported", snapshot.MissingReason ?? (snapshot.Vector.IsEmpty ? "descriptive_tags_missing" : null));
        }

        async Task<SeedTagSnapshot?> Seed(DiscoverySeedRequest request, bool scheduled)
        {
            string key = request.EntityId is { } entityKey ? $"entity:{entityKey:D}:{request.IsAlbum}:{request.IsRecording}" :
                $"legacy:{request.Name.Trim().ToLowerInvariant()}:{request.ArtistRecordingId}";
            if (seedReads.TryGetValue(key, out var cached)) return cached;
            SeedTagSnapshot? result;
            if (preparation is not null)
            {
                try { result = await preparation.GetAsync(request, false, ct); }
                catch (LinerNotes.Application.Common.Interfaces.Discovery.DiscoveryPreparationRequiredException) when (!scheduled || allowAcquisition)
                {
                    result = scheduled ? await preparation.GetAsync(request, true, ct) : null;
                }
                if (result is not null) seedReads[key] = result;
                return result;
            }
            if (!scheduled) return null;
            if (!allowAcquisition) throw new LinerNotes.Application.Common.Interfaces.Discovery.DiscoveryPreparationRequiredException();
            if (request.ArtistRecordingId is { } recordingHint && provider.SeedTags is IRecordingArtistSeedTagSource credited)
                return await credited.GetArtistFromRecordingAsync(recordingHint, request.Name, ct);
            if (request.IsRecording && request.EntityId is { } recording)
                return provider.SeedTags is IRecordingSeedTagSource direct ? await direct.GetRecordingAsync(recording, request.Name, ct)
                    : new(request.Name, WeightedTagVector.Empty, [], new(provider.Id, "seed.recording.tags", recording.ToString("D"), configuration.Origin, null, null), "direct_recording_tags_not_supported", []);
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
            stored.Add(snapshot.Store() with
            {
                SignalId = signal.Id,
                EntityIdentity = identity,
                TagScope = scope,
                Influence = influence,
                MissingReason = missing ?? snapshot.MissingReason
            });
            gaps.AddRange(snapshot.Gaps);
        }
    }
}
