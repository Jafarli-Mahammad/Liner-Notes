using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Infrastructure.Email;

public sealed record LocalEmailReceipt(Guid DigestId, Guid UserId, string Week, string Recipient,
    string TemplateVersion, string Fingerprint, string Token, string ContentHash);
public sealed record ParsedLocalEmail(LocalEmailReceipt Receipt, string PlainText, string Html, long Bytes);

/// <summary>Strict bounded local multipart format; authenticated metadata is a durable receipt.</summary>
public sealed class LocalEmailMessageSerializer(LocalEmailOptions options, IUnsubscribeTokenProtection protection, IUnsubscribeTokens tokens)
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private const string Prefix = "local-receipt-v1:";
    private static string BodyHash(string plain, string html) => Convert.ToHexStringLower(SHA256.HashData(
        Utf8.GetBytes(plain.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + plain + html)));

    private static string Fold(string value) => string.Join("\r\n ", Enumerable.Range(0, (value.Length+59)/60)
        .Select(i => value.Substring(i*60, Math.Min(60, value.Length-i*60))));
    private static string Date(string week)
    {
        var date = System.Globalization.ISOWeek.ToDateTime(int.Parse(week[..4], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(week[6..], System.Globalization.CultureInfo.InvariantCulture), DayOfWeek.Monday);
        return date.ToString("ddd, dd MMM yyyy HH:mm:ss +0000", System.Globalization.CultureInfo.InvariantCulture);
    }

    public byte[] Serialize(RenderedDigestEmail email)
    {
        options.Validate();
        var receipt = new LocalEmailReceipt(email.DigestId, email.UserId, email.Week, email.Recipient,
            email.TemplateVersion, email.Fingerprint, email.Token, BodyHash(email.PlainText, email.Html));
        string protectedReceipt = protection.Protect(Prefix + JsonSerializer.Serialize(receipt));
        string boundary = "liner_" + email.DigestId.ToString("N");
        var mime = new StringBuilder()
            .Append("From: ").Append(options.FromAddress).Append("\r\nTo: ").Append(email.Recipient)
            .Append("\r\nSubject: =?utf-8?B?").Append(Convert.ToBase64String(Utf8.GetBytes("Liner Notes " + email.Week))).Append("?=")
            .Append("\r\nMessage-ID: <").Append(email.DigestId.ToString("N")).Append('@').Append(options.MessageDomain).Append('>')
            .Append("\r\nMIME-Version: 1.0\r\nX-Liner-Receipt: ").Append(Fold(protectedReceipt))
            .Append("\r\nDate: ").Append(Date(email.Week)).Append("\r\nContent-Type: multipart/alternative; boundary=\"").Append(boundary).Append("\"\r\n\r\n");
        foreach (var (type, content) in new[] { ("text/plain", email.PlainText), ("text/html", email.Html) })
        {
            mime.Append("--").Append(boundary).Append("\r\nContent-Type: ").Append(type)
                .Append("; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n");
            string encoded = Convert.ToBase64String(Utf8.GetBytes(content));
            for (int i=0; i<encoded.Length; i+=76) mime.Append(encoded.AsSpan(i, Math.Min(76, encoded.Length-i))).Append("\r\n");
        }
        mime.Append("--").Append(boundary).Append("--\r\n");
        byte[] result = Utf8.GetBytes(mime.ToString());
        if (result.Length > options.MessageByteLimit || protectedReceipt.Length > 8192) throw new GenerationStoppedException("email_message_too_large");
        return result;
    }

    public ParsedLocalEmail Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > options.MessageByteLimit) throw new GenerationStoppedException("email_receipt_invalid");
        try
        {
            string mime = Utf8.GetString(bytes);
            int split = mime.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (split <= 0 || split > 16_384) throw new FormatException();
            var headers = mime[..split].Replace("\r\n ", "", StringComparison.Ordinal).Split("\r\n").ToDictionary(line => line[..line.IndexOf(':')], line => line[(line.IndexOf(':')+1)..].Trim(), StringComparer.Ordinal);
            string value = protection.Unprotect(headers["X-Liner-Receipt"]);
            if (!value.StartsWith(Prefix, StringComparison.Ordinal)) throw new FormatException();
            var receipt = JsonSerializer.Deserialize<LocalEmailReceipt>(value[Prefix.Length..]) ?? throw new FormatException();
            if (receipt.DigestId == Guid.Empty || receipt.UserId == Guid.Empty || string.IsNullOrEmpty(receipt.Week) ||
                string.IsNullOrEmpty(receipt.Recipient) || string.IsNullOrEmpty(receipt.Fingerprint) || receipt.TemplateVersion != DigestEmailRenderer.Version ||
                !tokens.TryRead(receipt.Token, out var owner) || owner != receipt.UserId ||
                headers["To"] != receipt.Recipient || headers["From"] != options.FromAddress ||
                headers["Message-ID"] != $"<{receipt.DigestId:N}@{options.MessageDomain}>" || headers["MIME-Version"] != "1.0" ||
                headers.Count != 8 || headers["Date"] != Date(receipt.Week) ||
                headers["Subject"] != "=?utf-8?B?" + Convert.ToBase64String(Utf8.GetBytes("Liner Notes " + receipt.Week)) + "?=") throw new FormatException();
            LocalEmailOptions.ValidateAddress(receipt.Recipient);
            string boundary = "liner_" + receipt.DigestId.ToString("N");
            if (headers["Content-Type"] != $"multipart/alternative; boundary=\"{boundary}\"") throw new FormatException();
            var parts = mime[(split+4)..].Split("--" + boundary);
            if (parts.Length != 4 || parts[0] != "" || parts[3] != "--\r\n") throw new FormatException();
            string Decode(string part, string type)
            {
                string prefix = $"\r\nContent-Type: {type}; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n";
                if (!part.StartsWith(prefix, StringComparison.Ordinal) || !part.EndsWith("\r\n", StringComparison.Ordinal)) throw new FormatException();
                var decoded = Convert.FromBase64String(part[prefix.Length..]);
                if (decoded.Length > options.AlternativeByteLimit) throw new FormatException();
                return Utf8.GetString(decoded);
            }
            string plain = Decode(parts[1], "text/plain"), html = Decode(parts[2], "text/html");
            if (BodyHash(plain, html) != receipt.ContentHash) throw new FormatException();
            return new(receipt, plain, html, bytes.Length);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or JsonException or CryptographicException or KeyNotFoundException or InvalidOperationException)
        { throw new GenerationStoppedException("email_receipt_invalid"); }
    }
}
