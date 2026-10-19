using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using LinerNotes.Presentation.Controllers;

namespace LinerNotes.Presentation.Discovery;

[Route("api/discovery/session")]
[EnableRateLimiting("catalog")]
public sealed class GuestController(IListenerContext listener, GuestLifecycle lifecycle, IAntiforgery antiforgery)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Bootstrap(CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        Response.Headers.CacheControl = "no-store";
        var profile = await listener.ResolveAsync(true, ct);
        var guest = await listener.GuestAsync(ct);
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new
        {
            csrfToken = tokens.RequestToken,
            profile = profile is null ? null :
            new { profile.Id, profile.UserId, profile.Revision, profile.IsGuest, profile.ExpiresAtUtc },
            guestImportAvailable = profile?.UserId is not null && guest is not null
        });
    }

    [HttpPost]
    [EnableRateLimiting("guest-start")]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        await listener.ValidateMutationAsync(ct);
        var existing = await listener.ResolveAsync(true, ct);
        if (existing is not null) return Ok(new { existing.Id, existing.IsGuest, existing.ExpiresAtUtc });
        if (User.Identity?.IsAuthenticated == true) return Unauthorized();
        var hash = listener.IssueCredential();
        try
        {
            var profile = await lifecycle.StartAsync(hash, ct);
            return Ok(new { profile.Id, profile.IsGuest, profile.ExpiresAtUtc });
        }
        catch { listener.RevokeCookie(); throw; }
    }

    [HttpGet("data")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        var profile = await listener.ResolveAsync(true, ct);
        if (profile is null) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(new { exportVersion = "guest-discovery-1", discovery = await lifecycle.ExportAsync(profile.Id, ct) });
    }

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        await listener.ValidateMutationAsync(ct);
        var profile = await listener.ResolveAsync(true, ct);
        if (profile is null || !profile.IsGuest || listener.CredentialHash is not { } hash) return Unauthorized();
        await lifecycle.ClearAsync(hash, ct);
        listener.RevokeCookie();
        return NoContent();
    }

    [HttpGet("import-preview")]
    public async Task<IActionResult> ImportPreview(CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        var account = await listener.ResolveAsync(false, ct);
        var guest = await listener.GuestAsync(ct);
        if (account?.UserId is null || guest is null) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        var saved = LinerNotes.Application.Features.Discovery.OnboardingState.Read(account.OnboardingJson);
        var incoming = LinerNotes.Application.Features.Discovery.OnboardingState.Read(guest.OnboardingJson);
        return Ok(new { account = saved, guest = incoming, conflicts = saved is not null && incoming is not null && saved.ConflictsWith(incoming), policy = "keep-account-answers-fill-unanswered" });
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(CancellationToken ct)
    {
        if (!listener.Enabled) return NotFound();
        await listener.ValidateMutationAsync(ct);
        var profile = await listener.ResolveAsync(true, ct);
        if (profile?.UserId is not { } userId || listener.CredentialHash is not { } hash) return Unauthorized();
        await lifecycle.ImportAsync(hash, userId, ct);
        listener.RevokeCookie();
        return NoContent();
    }
}
