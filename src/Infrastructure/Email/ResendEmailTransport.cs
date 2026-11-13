using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace LinerNotes.Infrastructure.Email;

public sealed class ResendEmailTransport(HttpClient client, IConfiguration configuration, EmailDeliveryOptions options) : IEmailTransport
{
    public async Task<EmailTransportResult> SendAsync(EmailPayload payload, string idempotencyKey, CancellationToken ct)
    {
        options.Validate();
        var key = configuration["EmailDelivery:ApiKey"];
        if (!options.Enabled || string.IsNullOrWhiteSpace(key)) return new("rejected");
        if (!System.Net.Mail.MailAddress.TryCreate(payload.To, out var recipient) || recipient.Address != payload.To ||
            payload.To.Any(char.IsControl) ||
            !System.Net.Mail.MailAddress.TryCreate(payload.From ?? options.FromAddress, out var sender) || sender.Address != (payload.From ?? options.FromAddress) ||
            (payload.From ?? options.FromAddress).Any(char.IsControl) || payload.Subject.Length > 998 || payload.Subject.Any(char.IsControl) ||
            payload.Headers.Any(pair => pair.Key is not ("List-Unsubscribe" or "List-Unsubscribe-Post") || pair.Value.Any(char.IsControl))) return new("rejected");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(new
        {
            from = payload.From ?? options.FromAddress,
            to = new[] { payload.To },
            subject = payload.Subject,
            text = payload.Text,
            html = payload.Html,
            headers = payload.Headers
        });
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            return new("retry", RetryAfter: Retry(response.Headers.RetryAfter));
        if (response.StatusCode == HttpStatusCode.Conflict) return new("unknown");
        if (!response.IsSuccessStatusCode) return new("rejected");
        using var json = await ReadAsync(response, ct);
        return json.RootElement.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 and <= 200 } value
            ? new("accepted", value) : new("unknown");
    }
    public async Task<EmailTransportResult> ReconcileAsync(string messageId, CancellationToken ct)
    {
        if (messageId.Length is < 1 or > 200 || !Guid.TryParse(messageId, out _)) return new("rejected");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.resend.com/emails/" + Uri.EscapeDataString(messageId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration["EmailDelivery:ApiKey"]);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) return new("unknown");
        using var json = await ReadAsync(response, ct);
        return new(json.RootElement.TryGetProperty("last_event", out var state) ? state.GetString() ?? "unknown" : "unknown", messageId);
    }
    private static TimeSpan? Retry(RetryConditionHeaderValue? value) => value?.Delta is { } delta ? delta :
        value?.Date is { } date ? date - DateTimeOffset.UtcNow : null;
    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var bytes = new byte[65_537]; int total = 0, read;
        while (total < bytes.Length && (read = await stream.ReadAsync(bytes.AsMemory(total), ct)) > 0) total += read;
        if (total == bytes.Length) throw new HttpRequestException("email_response_too_large");
        try { return JsonDocument.Parse(bytes.AsMemory(0, total)); }
        catch (JsonException) { throw new HttpRequestException("email_response_invalid"); }
    }
}
