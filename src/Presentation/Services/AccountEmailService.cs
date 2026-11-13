using System.Net;
using Microsoft.AspNetCore.DataProtection;
using System.Text;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Services;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.Domain.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.Presentation.Services;

public sealed class ConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions
{ public ConfirmationTokenProviderOptions() { Name = "LinerNotes.Confirmation"; TokenLifespan = TimeSpan.FromHours(24); } }
public sealed class RecoveryTokenProviderOptions : DataProtectionTokenProviderOptions
{ public RecoveryTokenProviderOptions() { Name = "LinerNotes.Recovery"; TokenLifespan = TimeSpan.FromHours(1); } }
public sealed class ConfirmationTokenProvider(IDataProtectionProvider protection, Microsoft.Extensions.Options.IOptions<ConfirmationTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger) : DataProtectorTokenProvider<ApplicationUser>(protection, options, logger);
public sealed class RecoveryTokenProvider(IDataProtectionProvider protection, Microsoft.Extensions.Options.IOptions<RecoveryTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger) : DataProtectorTokenProvider<ApplicationUser>(protection, options, logger);

public sealed class AccountEmailService(UserManager<ApplicationUser> users, EmailQueue queue, EmailDeliveryOptions options, IEmailDispatchStore store)
{
    public async Task RequestAsync(string email, EmailPurpose purpose, CancellationToken ct)
    {
        if (!queue.Enabled) return;
        var user = await users.FindByEmailAsync(email);
        if (user is null || user.IsDeleted || user.Email is null ||
            (await store.AccountAsync(user.Id, ct))?.Suppressed != false ||
            purpose == EmailPurpose.Confirmation && user.EmailConfirmed) return;
        var raw = purpose == EmailPurpose.Confirmation ? await users.GenerateEmailConfirmationTokenAsync(user) : await users.GeneratePasswordResetTokenAsync(user);
        var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(raw));
        var route = purpose == EmailPurpose.Confirmation ? "confirm-email" : "reset-password";
        var link = options.PublicOrigin + "/account/" + route + "#id=" + user.Id.ToString("D") + "&token=" + Uri.EscapeDataString(token);
        var title = purpose == EmailPurpose.Confirmation ? "Confirm your Liner Notes email" : "Reset your Liner Notes password";
        var text = title + "\n\nOpen this link and submit the form: " + link + "\nIf you did not request this, ignore this message. Your weekly email choice is unchanged.";
        var html = "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>" + WebUtility.HtmlEncode(title) +
            "</title><main><h1>" + WebUtility.HtmlEncode(title) + "</h1><p><a href=\"" + WebUtility.HtmlEncode(link) +
            "\">Continue securely</a></p><p>If you did not request this, ignore this message. Your weekly email choice is unchanged.</p></main></html>";
        await queue.EnqueueAsync(user.Id, null, purpose, route + ":" + Guid.NewGuid().ToString("N"),
            new(user.Email, title, text, html, new Dictionary<string, string>(), [link], SecurityStamp: user.SecurityStamp),
            purpose == EmailPurpose.Confirmation ? TimeSpan.FromHours(24) : TimeSpan.FromHours(1), TimeSpan.FromMinutes(1), ct);
    }
    public async Task<bool> ConsumeAsync(Guid id, string encodedToken, string? password)
    {
        if (id == Guid.Empty || encodedToken.Length is < 1 or > 8192) return false;
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken)); } catch (FormatException) { return false; }
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null || user.IsDeleted) return false;
        if (password is null)
        {
            if (user.EmailConfirmed) return await users.VerifyUserTokenAsync(user, users.Options.Tokens.EmailConfirmationTokenProvider, "EmailConfirmation", token);
            return (await users.ConfirmEmailAsync(user, token)).Succeeded;
        }
        return (await users.ResetPasswordAsync(user, token, password)).Succeeded;
    }
}
