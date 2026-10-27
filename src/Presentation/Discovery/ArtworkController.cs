using LinerNotes.Application.Common.Interfaces.Discovery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LinerNotes.Presentation.Discovery;

[ApiController]
[Route("api/artwork/recordings")]
[EnableRateLimiting("catalog")]
public sealed class ArtworkController(IRecordingArtwork artwork) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [HttpHead("{id:guid}")]
    public IActionResult Read(Guid id)
    {
        var cover = artwork.GetCached(id);
        var state = cover is not null ? RecordingArtworkState.Ready : artwork.Request(id);
        if (cover is null && state == RecordingArtworkState.Ready)
        {
            cover = artwork.GetCached(id);
            if (cover is null) state = RecordingArtworkState.Pending;
        }
        Response.Headers["X-Artwork-State"] = state.ToString().ToLowerInvariant();
        var seconds = cover?.ExpiresAtUtc is { } expiry ? Math.Clamp((int)(expiry - DateTimeOffset.UtcNow).TotalSeconds, 0, 3600) : 3600;
        Response.Headers.CacheControl = cover is null || !cover.CanPersist ? "no-store" : $"public,max-age={seconds}";
        if (state == RecordingArtworkState.Pending) Response.Headers.RetryAfter = "2";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return cover is null
            ? File("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"80\" height=\"80\" viewBox=\"0 0 500 500\"><rect width=\"500\" height=\"500\" fill=\"#e7e3db\"/><circle cx=\"250\" cy=\"250\" r=\"140\" fill=\"#ccc6ba\"/><circle cx=\"250\" cy=\"250\" r=\"35\" fill=\"#e7e3db\"/></svg>"u8.ToArray(), "image/svg+xml")
            : File(cover.Bytes, cover.ContentType);
    }
}
