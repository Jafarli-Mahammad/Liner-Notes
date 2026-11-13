using System.Net;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Features.Subscribers.Commands.UnsubscribeUser;
using LinerNotes.Infrastructure.Email;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinerNotes.Presentation.Controllers;

[AllowAnonymous]
[Route("api/digests/unsubscribe")]
public sealed class UnsubscribeController(IUnsubscribeTokens tokens, LocalEmailOptions options,
    IWebHostEnvironment environment, ISender mediator, EmailDeliveryOptions? delivery = null) : ControllerBase
{
    private bool Available()
    {
        if (delivery?.Enabled == true) { try { delivery.Validate(); return true; } catch (InvalidOperationException) { return false; } }
        if (!environment.IsDevelopment() || !options.Enabled) return false;
        try { options.Origin(); return true; } catch (InvalidOperationException) { return false; }
    }

    [HttpGet]
    public IActionResult Confirm([FromQuery] string? token)
    {
        if (!Available()) return NotFound();
        if (!tokens.TryRead(token, out _)) return BadRequest("Invalid unsubscribe link.");
        return Content("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>Unsubscribe · Liner Notes</title>" +
            "<main><h1>Unsubscribe from weekly emails</h1><p>Your account and taste data will stay available.</p>" +
            "<form method=\"post\" action=\"/api/digests/unsubscribe\"><input type=\"hidden\" name=\"token\" value=\"" +
            WebUtility.HtmlEncode(token) + "\"><button type=\"submit\">Confirm unsubscribe</button></form></main></html>", "text/html; charset=utf-8");
    }

    [HttpPost]
    [RequestSizeLimit(8192)]
    public async Task<IActionResult> Unsubscribe([FromForm] string? token, CancellationToken ct)
    {
        if (!Available()) return NotFound();
        token ??= Request.Query["token"].FirstOrDefault();
        if (!tokens.TryRead(token, out var id)) return BadRequest("Invalid unsubscribe link.");
        await mediator.Send(new UnsubscribeUserCommand(id), ct);
        return Content("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>Unsubscribed · Liner Notes</title><main><h1>Unsubscribe confirmed</h1><p>Future weekly emails are stopped. Your account and taste remain available. Messages already accepted for delivery cannot be recalled.</p></main></html>", "text/html; charset=utf-8");
    }
}
