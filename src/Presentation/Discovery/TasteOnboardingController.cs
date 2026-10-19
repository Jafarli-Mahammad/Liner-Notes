using LinerNotes.Application.Features.Discovery;
using LinerNotes.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LinerNotes.Presentation.Discovery;

[Route("api/discovery/onboarding")]
[EnableRateLimiting("catalog")]
public sealed class TasteOnboardingController(IListenerContext listener, SaveTasteOnboarding save) : ApiControllerBase
{
    [HttpPut]
    public async Task<IActionResult> Save(SaveOnboardingRequest request, CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        await listener.ValidateMutationAsync(ct);
        var profile = await listener.ResolveAsync(true, ct);
        if (profile is null) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(new { revision = await save.SaveAsync(profile.Id, request, ct) });
    }

    [HttpGet("tags")]
    public IActionResult Tags(string? query = null, string? category = null, int offset = 0, int limit = 24)
    {
        if (!listener.Enabled) return NotFound();
        return Ok(TasteVocabulary.Search(query, category, offset, limit));
    }
}
