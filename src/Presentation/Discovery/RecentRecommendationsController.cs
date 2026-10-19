using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LinerNotes.Presentation.Discovery;

[Route("api/discovery/recent")]
[EnableRateLimiting("catalog")]
public sealed class RecentRecommendationsController(IListenerContext listener, IRecentRecommendations recent, TimeProvider clock) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(CancellationToken ct, long? before = null, int limit = 20)
    {
        if (!listener.Enabled) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return Ok(await recent.ReadAsync(before, limit, clock.GetUtcNow(), ct));
    }
}
