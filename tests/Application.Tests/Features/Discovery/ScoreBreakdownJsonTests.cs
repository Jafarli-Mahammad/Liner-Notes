using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO.Compression;
using System.Security.Cryptography;
using LinerNotes.Application.Common.Mappings;
using LinerNotes.Domain.Scoring;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Discovery;

public sealed class ScoreBreakdownJsonTests
{
    [Fact]
    public void LegacyGzipEnvelopeRetainsEveryFieldAndStillValidatesItsChecksum()
    {
        var original = LargeReceipt();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(original);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true)) gzip.Write(bytes);
        var envelope = new { Format = "linernotes-score-json-gzip-v1", Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), Payload = Convert.ToBase64String(output.ToArray()) };
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(ScoreBreakdownJson.Deserialize(JsonSerializer.Serialize(envelope))));
        Assert.Throws<JsonException>(() => ScoreBreakdownJson.Deserialize(JsonSerializer.Serialize(envelope with { Sha256 = new string('0', 64) })));
    }

    [Fact]
    public void LargeEvidenceFitsExistingStorageBoundWithoutLosingAnyReceiptFields()
    {
        var value = LargeReceipt();
        var canonical = JsonSerializer.Serialize(value);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(canonical) > 100_000);
        var stored = ScoreBreakdownJson.Serialize(value);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(stored) < 100_000);
        Assert.Equal(canonical, JsonSerializer.Serialize(ScoreBreakdownJson.Deserialize(stored)));
        Assert.Equal(300, ScoreBreakdownJson.Deserialize(stored).UnknownFields!.Count);
    }

    [Fact]
    public void ExistingReceiptsRetainTheirExactStoredFormat()
    {
        var value = new ScoreBreakdown { UnknownFields = new() { ["future-null"] = JsonSerializer.SerializeToElement<object?>(null) } };
        var json = JsonSerializer.Serialize(value);
        Assert.Equal(json, ScoreBreakdownJson.Serialize(value));
        Assert.Equal(json, JsonSerializer.Serialize(ScoreBreakdownJson.Deserialize(json)));
    }

    [Fact]
    public void ModifiedPackedReceiptsFailChecksumValidation()
    {
        var envelope = JsonNode.Parse(ScoreBreakdownJson.Serialize(LargeReceipt()))!;
        envelope["Sha256"] = new string('0', 64);
        Assert.Throws<JsonException>(() => ScoreBreakdownJson.Deserialize(envelope.ToJsonString()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PackedReceiptsCannotExpandPastTheReaderBound(bool brotli)
    {
        var bytes = new byte[4 * 1024 * 1024 + 1];
        using var output = new MemoryStream();
        using (Stream compressed = brotli ? new BrotliStream(output, CompressionLevel.Optimal, true) : new GZipStream(output, CompressionLevel.Optimal, true)) compressed.Write(bytes);
        var json = JsonSerializer.Serialize(new
        {
            Format = brotli ? "linernotes-score-json-brotli-v1" : "linernotes-score-json-gzip-v1",
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            Payload = Convert.ToBase64String(output.ToArray())
        });
        Assert.Throws<JsonException>(() => ScoreBreakdownJson.Deserialize(json));
    }

    [Fact]
    public void UnknownPackedFormatsAreNotReadAsZeroScores()
    {
        var envelope = JsonNode.Parse(ScoreBreakdownJson.Serialize(LargeReceipt()))!;
        envelope["Format"] = "linernotes-score-json-unsupported-v2";
        Assert.Throws<JsonException>(() => ScoreBreakdownJson.Deserialize(envelope.ToJsonString()));
    }

    [Fact]
    public void CompressionDoesNotBypassExpandedOrStoredWriteLimits()
    {
        var expanded = new ScoreBreakdown { UnknownFields = new() { ["large"] = JsonSerializer.SerializeToElement(new string('x', 4 * 1024 * 1024)) } };
        Assert.Equal("snapshot_limit", Assert.Throws<LinerNotes.Application.Common.Interfaces.Recommendation.GenerationStoppedException>(() => ScoreBreakdownJson.Serialize(expanded)).Message);
        var random = new byte[110_000]; new Random(123).NextBytes(random);
        var incompressible = new ScoreBreakdown { UnknownFields = new() { ["large"] = JsonSerializer.SerializeToElement(Convert.ToBase64String(random)) } };
        Assert.Equal("snapshot_limit", Assert.Throws<LinerNotes.Application.Common.Interfaces.Recommendation.GenerationStoppedException>(() => ScoreBreakdownJson.Serialize(incompressible)).Message);
    }

    private static ScoreBreakdown LargeReceipt() => new()
    {
        UnknownFields = Enumerable.Range(0, 300).ToDictionary(i => "full-source-evidence-" + i,
            i => JsonSerializer.SerializeToElement(new
            {
                response = new StoredResponse("ListenBrainz", "metadata.artist", "00000000-0000-4000-8000-000000000001", "Live", new string('a', 64), DateTimeOffset.UnixEpoch),
                tag = "descriptive-observed-tag-" + i,
                votes = new StoredObservation("20", "20", null),
                extra = new string('x', 150)
            }))
    };
}
