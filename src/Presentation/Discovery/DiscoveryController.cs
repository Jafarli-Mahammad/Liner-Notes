using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Domain.Enums;
using LinerNotes.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LinerNotes.Presentation.Discovery;

[Route("api/discovery")]
[EnableRateLimiting("catalog")]
public sealed class DiscoveryController(IListenerContext listener, IDiscoveryReader reader,
    IDiscoveryJobStore jobs, DiscoveryRuntimeOptions options, TimeProvider clock) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        Response.Headers.CacheControl = "no-store";
        var profile = await listener.ResolveAsync(true, ct);
        var page = profile is null ? null : await reader.ReadAsync(profile.Id, clock.GetUtcNow(), TimeSpan.FromSeconds(options.CooldownSeconds), ct);
        return Ok(new { page, styles = SelectDiscoveryTaste.Styles, cooldownSeconds = options.CooldownSeconds });
    }

    public sealed record BatchRequest(long Revision, string RequestKey, DiscoveryTasteSelection? Selection = null);
    [HttpPost("batches")]
    public async Task<IActionResult> Submit([FromBody] BatchRequest request, CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        await listener.ValidateMutationAsync(ct);
        var profile = await listener.ResolveAsync(true, ct);
        if (profile is null) return Unauthorized();
        var job = await Mediator.Send(new SubmitDiscovery(profile.Id, request.Revision, request.RequestKey, request.Selection), ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Retry-After"] = "2";
        return Accepted(DiscoveryJobStatus.From(job));
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<IActionResult> Status(Guid id, CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        var profile = await listener.ResolveAsync(true, ct);
        if (profile is null) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        var job = await jobs.FindAsync(profile.Id, id, clock.GetUtcNow(), ct);
        if (job is null || job.Kind != LinerNotes.Domain.Discovery.DiscoveryJobKind.Generation) return NotFound();
        if (job.IsUnfinished) Response.Headers["Retry-After"] = "2";
        return Ok(DiscoveryJobStatus.From(job));
    }

    [HttpGet("batches/{id:guid}")]
    public async Task<IActionResult> Batch(Guid id, CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        var profile = await listener.ResolveAsync(true, ct);
        if (profile is null) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        var batch = await reader.ReadBatchAsync(profile.Id, id, clock.GetUtcNow(), ct);
        return batch is null ? NotFound() : Ok(batch);
    }

    public sealed record FeedbackRequest(long Revision, UserFeedback Feedback, int? Rating = null, string? Comment = null);
    [HttpPost("recommendations/{id:guid}/feedback")]
    public async Task<IActionResult> Feedback(Guid id, [FromBody] FeedbackRequest request, CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        await listener.ValidateMutationAsync(ct);
        var profile = await listener.ResolveAsync(true, ct);
        if (profile is null) return Unauthorized();
        long revision = await Mediator.Send(new RecordDiscoveryFeedback(profile.Id, id, request.Revision, request.Feedback, request.Rating, request.Comment), ct);
        return Ok(new { revision });
    }
}
