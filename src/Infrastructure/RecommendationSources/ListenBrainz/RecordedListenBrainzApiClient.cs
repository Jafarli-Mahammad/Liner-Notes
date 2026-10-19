using System.Collections.Immutable;
using System.Security.Cryptography;
using LinerNotes.Application.Common.Models.Recommendation;

namespace LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;

/// <summary>Immutable, hash-verified replay. Caller supplies bytes; no file discovery or network fallback.</summary>
public sealed class RecordedListenBrainzResponse
{
    public ListenBrainzRequest Request { get; }
    internal ImmutableArray<byte> Body { get; }
    public DateTimeOffset RetrievedAtUtc { get; }

    public RecordedListenBrainzResponse(ListenBrainzRequest request, ReadOnlySpan<byte> body,
        string expectedSha256, DateTimeOffset retrievedAtUtc)
    {
        request.Validate();
        if (body.Length > ListenBrainzResponse.MaximumBytes ||
            Convert.ToHexStringLower(SHA256.HashData(body)) != expectedSha256)
            throw new ArgumentException("listenbrainz_recording_invalid");
        Request = request;
        Body = ImmutableArray.Create(body.ToArray());
        RetrievedAtUtc = retrievedAtUtc.ToUniversalTime();
    }
}

public sealed class RecordedListenBrainzApiClient(IEnumerable<RecordedListenBrainzResponse> recordings) : IListenBrainzApiClient
{
    private readonly ImmutableArray<RecordedListenBrainzResponse> entries = recordings.ToImmutableArray();
    private readonly Dictionary<string, int> cursors = new(StringComparer.Ordinal);

    public Task<ListenBrainzResponse> SendAsync(ListenBrainzRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        request.Validate();
        var matches = entries.Where(e => e.Request == request).ToArray();
        int cursor = cursors.GetValueOrDefault(request.Key);
        if (cursor >= matches.Length)
            return Task.FromResult(ListenBrainzResponse.Gap(request, EvidenceOrigin.Recorded, "recording_request_not_available"));
        var recording = matches[cursor];
        cursors[request.Key] = cursor + 1;
        return Task.FromResult(ListenBrainzResponse.Parse(request, recording.Body.ToArray(), EvidenceOrigin.Recorded, recording.RetrievedAtUtc));
    }
}
