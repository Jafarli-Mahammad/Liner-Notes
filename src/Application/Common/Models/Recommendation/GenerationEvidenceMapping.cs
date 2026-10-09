using System.Globalization;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;

namespace LinerNotes.Application.Common.Models.Recommendation;

public static class GenerationEvidenceMapping
{
    public static StoredResponse Store(this ResponseReference value) => new(value.Provider, value.Method,
        value.RequestIdentity, value.Origin.ToString(), value.ResponseSha256, value.RetrievedAtUtc);
    public static StoredObservation Store<T>(this ObservedValue<T> value) where T : struct =>
        new(value.Value is { } supplied ? Convert.ToString(supplied, CultureInfo.InvariantCulture) : null,
            value.RawValue, value.MissingReason);
    public static StoredGap Store(this CoverageGap value) => new(value.Seed, value.Method, value.Reason, value.Response?.Store());
    public static StoredTag Store(this TagObservation value) => new(value.Name, value.Count.Store(), value.Url,
        value.Response.Store(), value.ExclusionReason, value.UpstreamPosition);
    public static StoredSeed Store(this SeedTagSnapshot value) => new(value.ArtistName, value.Vector.Weights,
        value.Tags.Select(Store).ToArray(), value.Response.Store(), value.MissingReason, value.Gaps.Select(Store).ToArray());
    public static StoredTasteSignal Store(this TasteSignal value) => new(value.Id, value.TargetType.ToString(),
        value.TargetValue, value.Weight, value.Source.ToString(), value.Context);
    public static StoredDiscoveryPath Store(this DiscoveryPath value) => new(value.Seed, value.Match.Store(),
        value.SimilarityResponse?.Store(), value.TrackResponse.Store(), value.UpstreamPosition, value.TrackUrl,
        value.ArtistUrl, value.TrackListeners.Store(), value.RawRank, value.DiscoveredArtist, value.SimilarityPosition);
}
