using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Features.Catalog;
using LinerNotes.Domain.Catalog;

namespace LinerNotes.Infrastructure.Catalog;

internal static class PublicRecordingCatalog
{
    private static readonly JsonSerializerOptions ArtifactJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task PublishAsync(IReadOnlyList<CatalogLookupResult> recordings, ImportCatalog import, CancellationToken ct)
    {
        foreach (var metadata in recordings)
        {
            if (metadata.Origin != CatalogOrigin.Public || metadata.CacheUntilUtc is null) continue;
            using var artifact = new MemoryStream();
            foreach (var row in metadata.Records)
            {
                await artifact.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(row, ArtifactJson), ct);
                artifact.WriteByte((byte)'\n');
            }
            artifact.Position = 0;
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(artifact, ct));
            static DateTimeOffset Microseconds(DateTimeOffset value) => new(value.Ticks - value.Ticks % 10, TimeSpan.Zero);
            var acquired = Microseconds(metadata.AcquiredAtUtc);
            var expires = Microseconds(metadata.CacheUntilUtc.Value);
            var identity = JsonSerializer.SerializeToUtf8Bytes(new { Version = "public-recordings-v1", hash,
                metadata.Source, metadata.SourceUri, metadata.ResponseSha256, acquired, expires });
            var publication = new CatalogPublication(new Guid(SHA256.HashData(identity).AsSpan(0, 16)),
                metadata.Source, "public-recordings-v1:" + metadata.ResponseSha256, metadata.SourceUri,
                CatalogOrigin.Public, "CC0-1.0", null, "MusicBrainz core metadata via ListenBrainz (CC0)",
                hash, artifact.Length, metadata.Records.Count, acquired, expires, isLookup: true);
            artifact.Position = 0;
            await import.RunAsync(publication, artifact, exactManifestApproved: true, supplementaryUseApproved: false, ct);
        }
    }
}
