using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
namespace LinerNotes.Infrastructure.Email;

public sealed class ResendEventVerifier(IConfiguration configuration, TimeProvider clock) : IEmailEventVerifier
{
    public EmailDeliveryEvent? Verify(byte[] body, string? id, string? timestamp, string? signature)
    {
        var secret = configuration["EmailDelivery:WebhookSecret"];
        if (body.Length > 65_536 || id is null || id.Length is < 1 or > 200 || timestamp is null || signature is null || signature.Length > 4096 ||
            secret?.StartsWith("whsec_", StringComparison.Ordinal) != true ||
            !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            Math.Abs((double)clock.GetUtcNow().ToUnixTimeSeconds() - seconds) > 300) return null;
        try
        {
            var key = Convert.FromBase64String(secret[6..]);
            if (key.Length < 16) return null;
            var prefix = Encoding.UTF8.GetBytes(id + "." + timestamp + ".");
            var signed = new byte[prefix.Length + body.Length]; prefix.CopyTo(signed, 0); body.CopyTo(signed, prefix.Length);
            var expected = HMACSHA256.HashData(key, signed);
            bool valid = signature.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(s => s.StartsWith("v1,", StringComparison.Ordinal))
                .Any(s => CryptographicOperations.FixedTimeEquals(expected, Convert.FromBase64String(s[3..])));
            if (!valid) return null;
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var type = root.TryGetProperty("type", out var eventType) ? eventType.GetString() : null;
            var outcome = type is not null && type.StartsWith("email.", StringComparison.Ordinal) ? type[6..] : "ignored";
            if (outcome is not ("delivered" or "bounced" or "complained" or "failed" or "suppressed")) return new("", "ignored", clock.GetUtcNow());
            var messageId = root.GetProperty("data").GetProperty("email_id").GetString();
            if (string.IsNullOrEmpty(messageId) || messageId.Length > 200) return null;
            var at = root.TryGetProperty("created_at", out var created) && created.TryGetDateTimeOffset(out var occurred) ? occurred : clock.GetUtcNow();
            return new(messageId, outcome, at);
        }
        catch (Exception error) when (error is FormatException or JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        { return null; }
    }
}
