using System.Text.Json;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Enums;

namespace LinerNotes.Application.Features.Discovery;

public sealed record DiscoveryJobStatus(Guid Id, string State, string ProgressCode, string? ErrorCode,
    Guid? BatchId, DateTimeOffset NextAttemptAtUtc)
{
    public static DiscoveryJobStatus From(DiscoveryJob job) => new(job.Id, job.State.ToString(), job.ProgressCode,
        job.ErrorCode, job.ResultBatchId, job.NextAttemptAtUtc);
}
public sealed record DiscoveryFavorite(Guid? CatalogRecordId, string Name, string? ArtistName, string Kind, string? Disambiguation = null,
    IReadOnlyList<LinerNotes.Application.Common.Interfaces.Catalog.CatalogReleaseDto>? Releases = null, Guid? SignalId = null);
public sealed record DiscoveryBatchSummary(Guid Id, string Kind, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc);
public sealed record DiscoveryPage(long Revision, IReadOnlyList<DiscoveryFavorite> Favorites, IReadOnlyList<string> Tags,
    IReadOnlyList<DiscoveryBatchSummary> Batches, DiscoveryJobStatus? CurrentJob, DateTimeOffset? NextRequestAtUtc, LinerNotes.Domain.Discovery.TasteOnboarding? Onboarding = null);
public sealed record DiscoveryPick(Guid Id, string Title, string ArtistName, string? Album, int Rank, string Explanation,
    IReadOnlyList<string> MatchedTags, UserFeedback Feedback, int? Rating, string? Comment, string? Mbid,
    string? SpotifyUrl, string? YoutubeUrl, IReadOnlyList<string> Sources);
public sealed record DiscoveryBatchView(Guid Id, string Kind, DateTimeOffset CreatedAtUtc, JsonElement? Coverage,
    IReadOnlyList<DiscoveryPick> Picks);
