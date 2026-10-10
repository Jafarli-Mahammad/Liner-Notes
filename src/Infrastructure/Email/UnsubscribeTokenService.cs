using System.Security.Cryptography;
using LinerNotes.Application.Common.Interfaces;

namespace LinerNotes.Infrastructure.Email;

public sealed class UnsubscribeTokenService(IUnsubscribeTokenProtection protection) : IUnsubscribeTokens
{
    private const string Prefix = "unsubscribe-v1:";
    public string Create(Guid userId) => userId != Guid.Empty
        ? protection.Protect(Prefix + userId.ToString("N")) : throw new ArgumentException("Account required.", nameof(userId));
    public bool TryRead(string? token, out Guid userId)
    {
        userId = default;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) return false;
        try
        {
            string value = protection.Unprotect(token);
            return value.StartsWith(Prefix, StringComparison.Ordinal) && Guid.TryParseExact(value[Prefix.Length..], "N", out userId) && userId != Guid.Empty;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException) { return false; }
    }
}
