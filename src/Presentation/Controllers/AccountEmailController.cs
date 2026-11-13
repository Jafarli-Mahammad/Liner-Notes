using System.ComponentModel.DataAnnotations;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Services;
using LinerNotes.Domain.Email;
using LinerNotes.Presentation.Filters;
using LinerNotes.Presentation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LinerNotes.Presentation.Controllers;

public sealed record RequestAccountEmail([Required, EmailAddress, MaxLength(256)] string Email);
public sealed record ConsumeAccountEmail(Guid UserId, [Required, MaxLength(8192)] string Token);
public sealed record ResetAccountPassword(Guid UserId, [Required, MaxLength(8192)] string Token, [Required, MinLength(12), MaxLength(1024)] string Password);
public sealed record EmailPreference(bool OptIn);
[ApiController, Route("api/account-email"), EnableRateLimiting("auth")]
public sealed class AccountEmailController(AccountEmailService accounts, IEmailDispatchStore store, EmailQueue queue,
    ICurrentUserService current) : ControllerBase
{
    [AllowAnonymous, HttpPost("request-confirmation"), ServiceFilter(typeof(DiscoveryWriteFilter))]
    public async Task<IActionResult> RequestConfirmation(RequestAccountEmail request, CancellationToken ct)
    {
        if (!queue.Enabled) return StatusCode(503, new { message = "Email is not enabled on this instance." });
        await accounts.RequestAsync(request.Email, EmailPurpose.Confirmation, ct);
        return Ok(new { message = "If this address is eligible, a confirmation message will be sent. Check your inbox and spam folder." });
    }
    [AllowAnonymous, HttpPost("request-reset"), ServiceFilter(typeof(DiscoveryWriteFilter))]
    public async Task<IActionResult> RequestReset(RequestAccountEmail request, CancellationToken ct)
    {
        if (!queue.Enabled) return StatusCode(503, new { message = "Email is not enabled on this instance." });
        await accounts.RequestAsync(request.Email, EmailPurpose.PasswordReset, ct);
        return Ok(new { message = "If this address is eligible, a password reset message will be sent. Check your inbox and spam folder." });
    }
    [AllowAnonymous, HttpPost("confirm"), ServiceFilter(typeof(DiscoveryWriteFilter))]
    public async Task<IActionResult> Confirm(ConsumeAccountEmail request) =>
        await accounts.ConsumeAsync(request.UserId, request.Token, null) ? NoContent() : BadRequest(new { message = "This link is invalid or expired. Request a new message." });
    [AllowAnonymous, HttpPost("reset"), ServiceFilter(typeof(DiscoveryWriteFilter))]
    public async Task<IActionResult> Reset(ResetAccountPassword request) =>
        await accounts.ConsumeAsync(request.UserId, request.Token, request.Password) ? NoContent() : BadRequest(new { message = "This link is invalid or expired, or the password was not accepted. Request a new message." });
    [Authorize, HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var account = await store.AccountAsync(current.UserId, ct);
        return account is null ? NotFound() : Ok(new
        {
            confirmed = account.Confirmed,
            optedIn = account.OptedIn && !account.Unsubscribed,
            suppressed = account.Suppressed,
            emailEnabled = queue.Enabled
        });
    }
    [Authorize, HttpPost("preference")]
    public async Task<IActionResult> Preference(EmailPreference request, CancellationToken ct)
    {
        return await store.SetPreferenceAsync(current.UserId, request.OptIn, ct) ? NoContent() : NotFound();
    }
}
