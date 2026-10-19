using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Digest;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Xunit;

namespace LinerNotes.Infrastructure.Tests;

public sealed class ListenBrainzRecordedInputTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "liner-lb-recorded-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private static string Sha(byte[] body) => Convert.ToHexStringLower(SHA256.HashData(body));

    [Theory]
    [InlineData("valid")]
    [InlineData("token")]
    [InlineData("provider")]
    [InlineData("changed")]
    public async Task ListenBrainzManifestKeepsApprovalAndCredentialBoundaries(string mode)
    {
        Directory.CreateDirectory(root);
        byte[] source = Encoding.UTF8.GetBytes("{\"approval\":{\"approved_manifest_sha256\":null,\"developer_reference\":\"synthetic-test-approval\"},\"provider\":\"ListenBrainz\",\"purpose\":\"provider-adapter-validation\",\"retention\":{\"expires_at_utc\":\"2099-01-01T00:00:00Z\"},\"status\":\"approved\"}");
        source = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(source).Replace("sha256\":null", "sha256\":\"" + Sha(source) + "\"", StringComparison.Ordinal));
        await File.WriteAllBytesAsync(Path.Combine(root, "source.json"), source);
        byte[] body = Encoding.UTF8.GetBytes(mode == "token" ? JsonSerializer.Serialize(new { token = Guid.NewGuid().ToString("D") }) : "[]");
        await File.WriteAllBytesAsync(Path.Combine(root, "response.json"), body);
        var user = Guid.NewGuid(); var week = new IsoWeek(2026, 42);
        var manifest = new LocalRecordedGenerationManifest(2, "local-digest-generation", "Recorded", user, week.Value,
            DateTimeOffset.UtcNow.AddHours(1), "synthetic-test", "source.json", Sha(source),
            [new("lb-radio.tags", "rock", "response.json", Sha(body), DateTimeOffset.UnixEpoch, 10, "Recorded")])
            { Provider = "listenbrainz" };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        string manifestPath = Path.Combine(root, "manifest.json");
        await File.WriteAllBytesAsync(manifestPath, bytes);
        var approval = new LocalRecordedInputApproval { InputRoot = root, ManifestPath = manifestPath, ApprovedManifestSha256 = Sha(bytes) };
        if (mode == "changed") await File.WriteAllTextAsync(Path.Combine(root, "response.json"), "{}");
        var loader = new LocalRecordedInputLoader(approval, TimeProvider.System);
        if (mode != "valid")
        {
            await Assert.ThrowsAsync<GenerationStoppedException>(() => loader.LoadAsync(user, week, default, mode == "provider" ? "lastfm" : "listenbrainz"));
            return;
        }
        var validated = await loader.LoadAsync(user, week, default, "listenbrainz");
        Assert.Empty(validated.Responses); Assert.Single(validated.ListenBrainzResponses);
        var inputs = new LocalRecordedInputs(); inputs.Set(validated);
        var replay = await new RecordedListenBrainzApiClient(inputs.ListenBrainzResponses!)
            .SendAsync(new(ListenBrainzRequestKind.TagRadio, "rock"));
        Assert.Equal(EvidenceOrigin.Recorded, replay.Reference.Origin); Assert.Empty(replay.Gaps);
    }
}
