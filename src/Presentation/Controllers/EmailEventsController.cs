using LinerNotes.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LinerNotes.Presentation.Controllers;

[ApiController, AllowAnonymous, Route("api/email-events")]
public sealed class EmailEventsController(IEmailEventVerifier verifier, IEmailDispatchStore store, EmailDeliveryOptions options) : ControllerBase
{
    [HttpPost, RequestSizeLimit(65_536)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        if (!options.Enabled) return NotFound();
        var bytes = new byte[65_537]; int total = 0, read;
        while (total < bytes.Length && (read = await Request.Body.ReadAsync(bytes.AsMemory(total), ct)) > 0) total += read;
        if (total == bytes.Length) return BadRequest();
        var value = verifier.Verify(bytes[..total], Request.Headers["svix-id"], Request.Headers["svix-timestamp"], Request.Headers["svix-signature"]);
        if (value is null) return Unauthorized();
        if (value.Outcome == "ignored") return NoContent();
        return await store.EventAsync(value, ct) ? NoContent() : StatusCode(503);
    }
}
