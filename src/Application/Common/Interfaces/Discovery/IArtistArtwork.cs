namespace LinerNotes.Application.Common.Interfaces.Discovery;

public sealed record ArtistPhoto(byte[] Bytes, string ContentType, string SourceUrl, string Credit,
    string License, string LicenseUrl, DateTimeOffset ExpiresAtUtc);

public interface IArtistArtwork
{
    ArtistPhoto? GetCached(Guid artistId);
    RecordingArtworkState Request(Guid artistId);
    TimeSpan? RetryDelay(Guid artistId);
}

public interface IArtistArtworkAcquirer
{
    Task<ArtistPhoto?> AcquireAsync(Guid artistId, CancellationToken ct);
}
