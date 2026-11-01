using LinerNotes.Application.Common.Interfaces.Discovery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LinerNotes.Presentation.Discovery;

[ApiController]
[Route("api/artwork/artists")]
[EnableRateLimiting("catalog")]
public sealed class ArtistArtworkController(IArtistArtwork artwork) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public IActionResult Read(Guid id)
    {
        if (artwork.GetCached(id) is { } photo)
            return Ok(new { imageUrl = $"/api/artwork/artists/{id:D}/image", photo.SourceUrl,
                photo.Credit, photo.License, photo.LicenseUrl });
        var state = artwork.Request(id);
        Response.Headers.CacheControl = "no-store";
        if (state == RecordingArtworkState.Pending) Response.Headers.RetryAfter = "3";
        return StatusCode(state == RecordingArtworkState.Pending ? 503 : 404);
    }

    [HttpGet("{id:guid}/image")]
    public IActionResult Image(Guid id)
    {
        if (artwork.GetCached(id) is not { } photo) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return File(photo.Bytes, photo.ContentType);
    }
}
