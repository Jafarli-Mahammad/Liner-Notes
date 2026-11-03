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
    private const string Format = "linernotes-score-json-brotli-v1";
    private const string LegacyFormat = "linernotes-score-json-gzip-v1";
    private sealed record Envelope(string Format, string Sha256, string Payload);

    public static byte[] SerializeToUtf8Bytes(ScoreBreakdown value)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(value);
        if (json.Length <= MaximumStoredBytes) return json;
        if (json.Length > MaximumExpandedBytes) throw new GenerationStoppedException("snapshot_limit");
        string hash = Convert.ToHexStringLower(SHA256.HashData(json));
        var stored = Pack(CompressionLevel.Optimal);
        if (stored.Length > MaximumStoredBytes) stored = Pack(CompressionLevel.SmallestSize);
        if (stored.Length > MaximumStoredBytes)
        {
            var failure = new GenerationStoppedException("snapshot_limit");
            failure.Data["SnapshotBytes"] = stored.Length;
            failure.Data["ExpandedBytes"] = json.Length;
            throw failure;
        }
        return stored;

        byte[] Pack(CompressionLevel level)
        {
            using var output = new MemoryStream();
            using (var compressed = new BrotliStream(output, level, leaveOpen: true)) compressed.Write(json);
            return JsonSerializer.SerializeToUtf8Bytes(new Envelope(Format, hash,
                Convert.ToBase64String(output.GetBuffer(), 0, checked((int)output.Length))));
        }

    }

    public static string Serialize(ScoreBreakdown value) => System.Text.Encoding.UTF8.GetString(SerializeToUtf8Bytes(value));

    public static ScoreBreakdown Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.TryGetProperty("FinalScore", out _) ||
            !document.RootElement.TryGetProperty("Format", out var format))
            return JsonSerializer.Deserialize<ScoreBreakdown>(json) ?? new ScoreBreakdown();
        if (format.ValueKind != JsonValueKind.String || format.GetString() is not (Format or LegacyFormat))
            throw new JsonException("unsupported_stored_score_format");
        var envelope = document.RootElement.Deserialize<Envelope>() ?? throw new JsonException("invalid_stored_score");
        if (envelope.Payload is null || envelope.Payload.Length > MaximumStoredBytes || envelope.Sha256 is not { Length: 64 })
            throw new JsonException("invalid_stored_score");
        try
        {
            using var input = new MemoryStream(Convert.FromBase64String(envelope.Payload));
            using Stream compressed = envelope.Format == LegacyFormat ? new GZipStream(input, CompressionMode.Decompress) : new BrotliStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = compressed.Read(buffer)) > 0)
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
