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
    public IActionResult Read(Guid id)
    {
        var cover = artwork.GetCached(id);
        Response.Headers.CacheControl = cover is null ? "public,max-age=15" : "public,max-age=3600";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return cover is null
            ? File("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"80\" height=\"80\" viewBox=\"0 0 500 500\"><rect width=\"500\" height=\"500\" fill=\"#e7e3db\"/><circle cx=\"250\" cy=\"250\" r=\"140\" fill=\"#ccc6ba\"/><circle cx=\"250\" cy=\"250\" r=\"35\" fill=\"#e7e3db\"/></svg>"u8.ToArray(), "image/svg+xml")
            : File(cover.Bytes, cover.ContentType);
    }
}
