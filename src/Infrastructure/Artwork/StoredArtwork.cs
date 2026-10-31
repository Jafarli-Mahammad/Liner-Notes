using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Models.Recommendation;

namespace LinerNotes.Infrastructure.Artwork;

// Public image cache only: no owner IDs or taste data. Writes use the existing storage admission.
public sealed class StoredArtwork(IPublicEvidenceStore store, IDiscoveryWriteCoordinator admission, TimeProvider clock) : IStoredArtwork
{
    private const string Version = "recording-artwork-v2";
    private static string Key(Guid id) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Version + ":" + id.ToString("D"))));

    public async Task<RecordingCover?> FindAsync(Guid recordingId, CancellationToken ct)
    {
        var document = await store.FindAsync(Key(recordingId), clock.GetUtcNow(), ct);
        if (document is null) return null;
        if (document.Kind != PublicEvidenceKind.Artwork || document.Provider != "CoverArtArchive" || document.Version != Version ||
            document.Origin != EvidenceOrigin.Live || document.PayloadJson.Length > 1_500_000 ||
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(document.PayloadJson))) != document.ContentSha256)
            return null;
        try
        {
            var cover = JsonSerializer.Deserialize<RecordingCover>(document.PayloadJson);
            return cover is { Bytes.Length: > 0, ContentType: not null, CanPersist: true } && cover.ExpiresAtUtc == document.ExpiresAtUtc &&
                RecordingArtwork.ValidImage(cover.Bytes, cover.ContentType) ? cover : null;
        }
        catch (Exception ex) when (ex is JsonException or FormatException) { return null; }
    }

    public async Task SaveAsync(Guid recordingId, RecordingCover cover, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (!cover.CanPersist || cover.ExpiresAtUtc is not { } expiry || expiry <= now || expiry > now.AddDays(1) ||
            !RecordingArtwork.ValidImage(cover.Bytes, cover.ContentType)) return;
        // PostgreSQL timestamps retain microseconds; payload and row expiry must compare exactly on restoration.
        expiry = new DateTimeOffset(expiry.Ticks - expiry.Ticks % 10, TimeSpan.Zero);
        if (expiry <= now) return;
        cover = cover with { ExpiresAtUtc = expiry };
        var payload = JsonSerializer.Serialize(cover);
        var document = new PublicEvidenceDocument(Key(recordingId), PublicEvidenceKind.Artwork, "CoverArtArchive", EvidenceOrigin.Live,
            Version, payload, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload))), now, expiry);
        await admission.ExecuteAsync(Encoding.UTF8.GetByteCount(payload) * 4L + 65_536, async token =>
        {
            var lease = await store.ClaimAsync(document.Key, PublicEvidenceKind.Artwork, now, TimeSpan.FromSeconds(60), token);
            if (lease is not null) await store.PublishAsync(lease, document, now, 64 * 1024 * 1024, 1000, token);
            return true;
        }, ct);
    }
}
