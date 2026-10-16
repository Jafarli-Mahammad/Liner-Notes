using LinerNotes.Application.Features.Catalog;
using LinerNotes.Domain.Catalog;
using LinerNotes.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LinerNotes.Presentation.Discovery;

[Route("api/catalog")]
[EnableRateLimiting("catalog")]
public sealed class CatalogController(IListenerContext listener) : ApiControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? query, [FromQuery] CatalogKind kind = CatalogKind.Artist,
        [FromQuery] int limit = 10, [FromQuery] int offset = 0, CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "public,max-age=30";
        return Ok(await Mediator.Send(new SearchCatalog(query ?? "", kind, limit, offset), ct));
    }

    public sealed record LookupRequest(string Query, CatalogKind Kind, string RequestKey);
    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup([FromBody] LookupRequest request, CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        await listener.ValidateMutationAsync(ct);
        var profile = await listener.ResolveAsync(true, ct);
        if (profile is null) return Unauthorized();
        return Accepted(await Mediator.Send(new SubmitCatalogLookup(profile.Id, request.Query, request.Kind, request.RequestKey), ct));
    }

    [HttpGet("lookups/{id:guid}")]
    public async Task<IActionResult> LookupStatus(Guid id, CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        var profile = await listener.ResolveAsync(false, ct);
        if (profile is null) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        var result = await Mediator.Send(new GetCatalogLookup(profile.Id, id), ct);
        return result is null ? NotFound() : Ok(result);
    }
}
