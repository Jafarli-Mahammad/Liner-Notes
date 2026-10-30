using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Domain.Scoring;

namespace LinerNotes.Application.Common.Mappings;

public static class ScoreBreakdownJson
{
    // The encoded receipt stays within the existing row budget; decoding restores every field.
    private const int MaximumStoredBytes = 100_000;
    private const int MaximumExpandedBytes = 4 * 1024 * 1024;
    private const string Format = "linernotes-score-json-gzip-v1";
    private sealed record Envelope(string Format, string Sha256, string Payload);

    public static byte[] SerializeToUtf8Bytes(ScoreBreakdown value)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(value);
        if (json.Length <= MaximumStoredBytes) return json;
        if (json.Length > MaximumExpandedBytes) throw new GenerationStoppedException("snapshot_limit");
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(json);
        var stored = JsonSerializer.SerializeToUtf8Bytes(new Envelope(Format,
            Convert.ToHexStringLower(SHA256.HashData(json)), Convert.ToBase64String(output.GetBuffer(), 0, checked((int)output.Length))));
        if (stored.Length > MaximumStoredBytes) throw new GenerationStoppedException("snapshot_limit");
        return stored;
    }

    public static string Serialize(ScoreBreakdown value) => System.Text.Encoding.UTF8.GetString(SerializeToUtf8Bytes(value));

    public static ScoreBreakdown Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.TryGetProperty("FinalScore", out _) ||
            !document.RootElement.TryGetProperty("Format", out var format))
            return JsonSerializer.Deserialize<ScoreBreakdown>(json) ?? new ScoreBreakdown();
        if (format.ValueKind != JsonValueKind.String || format.GetString() != Format)
            throw new JsonException("unsupported_stored_score_format");
        var envelope = document.RootElement.Deserialize<Envelope>() ?? throw new JsonException("invalid_stored_score");
        if (envelope.Payload is null || envelope.Payload.Length > MaximumStoredBytes || envelope.Sha256 is not { Length: 64 })
            throw new JsonException("invalid_stored_score");
        try
        {
            using var input = new MemoryStream(Convert.FromBase64String(envelope.Payload));
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = gzip.Read(buffer)) > 0)
            {
                if (output.Length + read > MaximumExpandedBytes) throw new JsonException("stored_score_expansion_limit");
                output.Write(buffer, 0, read);
            }
            var bytes = output.GetBuffer().AsSpan(0, checked((int)output.Length));
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != envelope.Sha256) throw new JsonException("stored_score_checksum_mismatch");
            return JsonSerializer.Deserialize<ScoreBreakdown>(bytes) ?? throw new JsonException("invalid_stored_score");
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException or IOException)
        { throw new JsonException("invalid_stored_score", ex); }
    }
}
