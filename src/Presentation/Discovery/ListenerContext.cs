using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Domain.Discovery;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LinerNotes.Presentation.Discovery;

public sealed class GuestDiscoveryOptions
{
    public bool Enabled { get; set; }
    public string? PublicOrigin { get; set; }
}

public interface IListenerContext
{
    bool Enabled { get; }
    Task<ListenerProfile?> ResolveAsync(bool touch, CancellationToken ct);
    Task<ListenerProfile?> GuestAsync(CancellationToken ct);
    Task ValidateMutationAsync(CancellationToken ct);
    string? CredentialHash { get; }
    string IssueCredential();
    void RevokeCookie();
}

public sealed class ListenerContext(IHttpContextAccessor accessor, IGuestProfileStore store,
    GuestLifecycle lifecycle, IAntiforgery antiforgery, IOptions<GuestDiscoveryOptions> options,
    IHostEnvironment environment, TimeProvider clock) : IListenerContext
{
    public const string CookieName = "liner.guest";
    private HttpContext Http => accessor.HttpContext ?? throw new InvalidOperationException("HTTP context required.");
    public bool Enabled => options.Value.Enabled;
    public string? CredentialHash => HashCredential(Http.Request.Cookies[CookieName]);

    public static string? HashCredential(string? credential)
    {
        if (credential is null || credential.Length != 64 || credential.Any(c => !char.IsAsciiHexDigit(c))) return null;
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(credential)));
    }

    public async Task<ListenerProfile?> ResolveAsync(bool touch, CancellationToken ct)
    {
        if (!Enabled) return null;
        // An authenticated account always takes precedence; a guest cookie never selects another owner.
        if (Http.User.Identity?.IsAuthenticated == true)
        {
            if (!Guid.TryParse(Http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) throw new UnauthorizedAccessException();
            return await store.FindAccountAsync(userId, ct);
        }
        var profile = CredentialHash is { } hash ? await lifecycle.ResolveGuestAsync(hash, touch, ct) : null;
        if (profile is not null && touch)
            Http.Response.Cookies.Append(CookieName, Http.Request.Cookies[CookieName]!, CookieOptions(profile.ExpiresAtUtc));
        return profile;
    }

    public Task<ListenerProfile?> GuestAsync(CancellationToken ct) => Enabled && CredentialHash is { } hash
        ? store.FindGuestAsync(hash, clock.GetUtcNow(), ct) : Task.FromResult<ListenerProfile?>(null);

    public async Task ValidateMutationAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Enabled) throw new InvalidOperationException("discovery_disabled");
        if (!Http.Request.IsHttps && !environment.IsDevelopment()) throw new UnauthorizedAccessException();
        var expected = options.Value.PublicOrigin;
        if (environment.IsDevelopment() && string.IsNullOrWhiteSpace(expected))
        {
            if (!Http.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
                Http.Request.Host.Host != "127.0.0.1" && Http.Request.Host.Host != "::1")
                throw new UnauthorizedAccessException();
            expected = $"{Http.Request.Scheme}://{Http.Request.Host}";
        }
        if (!Uri.TryCreate(expected, UriKind.Absolute, out var uri) || expected != uri.GetLeftPart(UriPartial.Authority) ||
            (!environment.IsDevelopment() && uri.Scheme != "https") ||
            !string.Equals(Http.Request.Headers.Origin, expected, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException();
        await antiforgery.ValidateRequestAsync(Http);
    }

    public string IssueCredential()
    {
        if (!Http.Request.IsHttps && !environment.IsDevelopment()) throw new UnauthorizedAccessException();
        var credential = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        Http.Response.Cookies.Append(CookieName, credential, CookieOptions());
        return HashCredential(credential)!;
    }

    public void RevokeCookie() => Http.Response.Cookies.Delete(CookieName, CookieOptions());
    private CookieOptions CookieOptions(DateTimeOffset? expiresAt = null) => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment() || Http.Request.IsHttps,
        SameSite = SameSiteMode.Strict, Path = "/", MaxAge = expiresAt is { } expiry
            ? TimeSpan.FromSeconds(Math.Max(0, (expiry - clock.GetUtcNow()).TotalSeconds)) : GuestLifecycle.Inactivity,
        IsEssential = true
    };
}
