using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;

namespace LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;

public sealed partial class ListenBrainzRecommendationProvider(IListenBrainzApiClient client)
    : IRecommendationSource, ICandidateHydrator, ISeedTagSource, ICanonicalRecommendationSource, ICanonicalSeedTagSource, IRecordingRecommendationSource, IRecordingSeedTagSource, IPublicEvidenceFreshness
{
    public string SourceName => "ListenBrainz";
    private readonly Dictionary<string, ListenBrainzResponse> responses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (RecordingMetadata Metadata, ResponseReference Response)> metadata = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ListenBrainzResponse> artistTags = new(StringComparer.Ordinal);

    private async Task<ListenBrainzResponse> SendAsync(ListenBrainzRequest request, CancellationToken ct)
    {
        if (responses.TryGetValue(request.Key, out var cached)) return cached;
        var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        if (response.Reference.Provider != request.Provider || response.Reference.Method != request.Method || response.Reference.RequestIdentity != request.Identity)
            throw new GenerationStoppedException("provider_response_provenance_mismatch");
        workingBytes = checked(workingBytes + (response.Data is { } data ? System.Text.Encoding.UTF8.GetByteCount(data.GetRawText()) * 8L : 1024));
        if (workingBytes > 32 * 1024 * 1024) throw new GenerationStoppedException("provider_working_set_limit");
        if (response.Reference.Origin != EvidenceOrigin.Synthetic)
        {
            if (response.SharedCacheUntilUtc is not { } deadline) sharedCachingProhibited = true;
            else if (SharedUntilUtc is null || deadline < SharedUntilUtc) SharedUntilUtc = deadline;
        }
        responses[request.Key] = response;
        return response;
    }

    public Task<IngestionResult<RawCandidateTrack>> GetCandidatesByArtistsAsync(IReadOnlyList<string> artistNames,
        int limitPerArtist = 10, CancellationToken cancellationToken = default) => DiscoverAsync(artistNames, limitPerArtist, false, cancellationToken);
    public Task<IngestionResult<RawCandidateTrack>> GetCandidatesByTagsAsync(IReadOnlyList<string> tags,
        int limitPerTag = 10, CancellationToken cancellationToken = default) => DiscoverAsync(tags, limitPerTag, true, cancellationToken);

    private async Task<IngestionResult<RawCandidateTrack>> DiscoverAsync(IReadOnlyList<string> seeds, int limit, bool byTag, CancellationToken ct, bool canonical = false, bool byRecording = false)
    {
        ct.ThrowIfCancellationRequested();
        if (limit is < 1 or > 10 || seeds.Count > 20) throw new ArgumentException("listenbrainz_discovery_limit");
        var candidates = new List<RawCandidateTrack>();
        var gaps = new List<CoverageGap>();
        foreach (string seed in seeds.Order(StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(seed) || seed.Length > 200 || seed.Any(char.IsControl))
            { gaps.Add(new(seed ?? "", "discovery", "invalid_seed")); continue; }
            string identity = seed.Trim().ToLowerInvariant();
            ListenBrainzResponse? search = null;
            ListenBrainzRequest request;
            if (byRecording) request = new(ListenBrainzRequestKind.SimilarRecordings, identity, limit);
            else if (byTag) request = new(ListenBrainzRequestKind.TagRadio, identity, limit);
            else if (canonical) request = new(ListenBrainzRequestKind.ArtistRadio, identity, limit);
            else
            {
                search = await SendAsync(new(ListenBrainzRequestKind.ArtistSearch, identity), ct).ConfigureAwait(false);
                gaps.AddRange(search.Gaps);
                var artist = ListenBrainzEvidenceParser.ResolveArtist(search, identity, out var reason);
                if (artist is null) { gaps.Add(new(identity, search.Reference.Method, reason!, search.Reference)); continue; }
                request = new(ListenBrainzRequestKind.ArtistRadio, artist.Mbid, limit);
            }
            var radio = await SendAsync(request, ct).ConfigureAwait(false);
            gaps.AddRange(radio.Gaps);
            var ids = ListenBrainzEvidenceParser.RecordingIds(radio, limit, out bool invalid);
            if (invalid) gaps.Add(new(identity, radio.Reference.Method, "invalid_discovery_shape_or_identifier", radio.Reference));
            if (ids.Count == 0) { gaps.Add(new(identity, radio.Reference.Method, "no_supported_tracks", radio.Reference)); continue; }
            var detail = await SendAsync(new(ListenBrainzRequestKind.RecordingMetadata, string.Join(',', ids)), ct).ConfigureAwait(false);
            gaps.AddRange(detail.Gaps);
            if (detail.Reference.Origin != radio.Reference.Origin || (search is not null && search.Reference.Origin != radio.Reference.Origin))
                throw new GenerationStoppedException("origin_mismatch");
            int position = 0;
            foreach (string id in ids)
            {
                position++;
                var track = ListenBrainzEvidenceParser.Recording(detail, id);
                if (track is null) { gaps.Add(new(id, detail.Reference.Method, "recording_metadata_missing", detail.Reference)); continue; }
                metadata.TryAdd(id, (track, detail.Reference));
                var path = new DiscoveryPath(identity, ObservedValue<double>.Missing("not_supplied_by_endpoint"),
                    radio.Reference, detail.Reference, position, "https://musicbrainz.org/recording/" + id, null,
                    ObservedValue<long>.Missing("unique_listeners_not_supplied"))
                { SupportingResponses = search is null ? [] : [search.Reference] };
                candidates.Add(new(track.Title, track.ArtistName, id, [path]));
            }
        }
        return new(candidates.OrderBy(c => c.CandidateKey, StringComparer.Ordinal), gaps);
    }

    public async Task<SeedTagSnapshot> GetAsync(string artistName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string name = artistName.Trim().ToLowerInvariant();
        var search = await SendAsync(new(ListenBrainzRequestKind.ArtistSearch, name), cancellationToken).ConfigureAwait(false);
        var artist = ListenBrainzEvidenceParser.ResolveArtist(search, name, out var reason);
        if (artist is null) return new(name, WeightedTagVector.Empty, [], search.Reference, reason,
            search.Gaps.Append(new(name, search.Reference.Method, reason!, search.Reference)).ToArray());
        var response = await SendAsync(new(ListenBrainzRequestKind.ArtistMetadata, artist.Mbid), cancellationToken).ConfigureAwait(false);
        artistTags[artist.Mbid] = response;
        if (response.Reference.Origin != search.Reference.Origin) throw new GenerationStoppedException("origin_mismatch");
        var tags = ListenBrainzEvidenceParser.ArtistTags([(artist.Mbid, response)]);
        return new(name, tags.Vector, tags.Tags, response.Reference, tags.MissingReason, search.Gaps.Concat(tags.Gaps).ToArray())
        { SupportingResponses = [search.Reference] };
    }

    public async Task<HydratedCandidateTrack?> HydrateCandidateAsync(RawCandidateTrack rawCandidate, CancellationToken cancellationToken = default)
        => (await HydrateCandidatesBatchAsync([rawCandidate], cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    public async Task<IngestionResult<HydratedCandidateTrack>> HydrateCandidatesBatchAsync(IReadOnlyList<RawCandidateTrack> rawCandidates,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (rawCandidates.Count > 400) throw new ArgumentException("listenbrainz_candidate_limit");
        var hydrated = new List<HydratedCandidateTrack>();
        var gaps = new List<CoverageGap>();
        if (rawCandidates.Select(c => c.Origin).Distinct().Count() > 1) throw new GenerationStoppedException("origin_mismatch");
        var needed = rawCandidates.Where(c => c.Mbid is not null && metadata.ContainsKey(c.Mbid))
            .SelectMany(c => metadata[c.Mbid!].Metadata.ArtistMbids).Distinct(StringComparer.Ordinal)
            .Where(id => !artistTags.ContainsKey(id)).Order(StringComparer.Ordinal).ToArray();
        foreach (var chunk in needed.Chunk(100))
        {
            var response = await SendAsync(new(ListenBrainzRequestKind.ArtistMetadata, string.Join(',', chunk)), cancellationToken).ConfigureAwait(false);
            foreach (string id in chunk) artistTags[id] = response;
        }
        foreach (var raw in rawCandidates.OrderBy(c => c.CandidateKey, StringComparer.Ordinal))
        {
            if (raw.Mbid is null || !metadata.TryGetValue(raw.Mbid, out var entry))
            { gaps.Add(new(raw.CandidateKey, "metadata.recording", "candidate_not_discovered_in_scope")); continue; }
            if (raw.Title != entry.Metadata.Title || raw.ArtistName != entry.Metadata.ArtistName)
            { gaps.Add(new(raw.CandidateKey, "metadata.recording", "candidate_metadata_identity_changed", entry.Response)); continue; }
            var tagInputs = new List<(string, ListenBrainzResponse)>();
            foreach (string artist in entry.Metadata.ArtistMbids)
            {
                var response = artistTags[artist];
                if (response.Reference.Origin != raw.Origin) throw new GenerationStoppedException("origin_mismatch");
                tagInputs.Add((artist, response));
            }
            var tags = ListenBrainzEvidenceParser.ArtistTags(tagInputs);
            var recordingResponse = responses.Values.FirstOrDefault(r => r.Reference == entry.Response);
            var directTags = recordingResponse is null ? null : RecordingTags(recordingResponse, Guid.Parse(raw.Mbid!), raw.Title);
            bool direct = directTags is { Vector.IsEmpty: false };

            gaps.AddRange(tags.Gaps);
            if (!direct && tags.Vector.IsEmpty)
            {
                gaps.Add(new(raw.CandidateKey, "metadata.artist", "no_valid_artist_tag_votes", entry.Response));
                continue;
            }
            hydrated.Add(new(Track.Create(raw.Title, raw.ArtistName, mbid: raw.Mbid), direct ? directTags!.Vector : tags.Vector, raw, direct ? directTags!.Tags : tags.Tags,
                direct ? entry.Response : tagInputs[0].Item2.Reference, direct ? directTags!.MissingReason : tags.MissingReason, tags.Gaps)
            {
                TagScope = direct ? "recording" : "artist",
                TagNormalizationVersion = ListenBrainzEvidenceParser.NormalizationVersion,
                SupportingResponses = tagInputs.Select(t => t.Item2.Reference).Append(entry.Response).Distinct().ToArray()
            });
        }
        return new(hydrated, gaps);
    }
}
