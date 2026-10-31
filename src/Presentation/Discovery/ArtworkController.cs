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
        if (state == RecordingArtworkState.Pending)
            Response.Headers.RetryAfter = Math.Clamp((int)Math.Ceiling((artwork.RetryDelay(id) ?? TimeSpan.FromSeconds(2)).TotalSeconds), 1, 86400)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        // A placeholder success here could be mistaken for ready artwork after a HEAD/GET expiry race.
        if (cover is null) return StatusCode(state == RecordingArtworkState.Pending ? 503 : 404);
        return File(cover.Bytes, cover.ContentType);
    }
}
