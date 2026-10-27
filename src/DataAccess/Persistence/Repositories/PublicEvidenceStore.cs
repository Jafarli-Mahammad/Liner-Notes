using System.Security.Cryptography;
using System.Text;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.Persistence.Accounting;
using Microsoft.EntityFrameworkCore;
namespace LinerNotes.DataAccess.Persistence.Repositories;
public sealed class PublicEvidenceStore(DataContext db) : IPublicEvidenceStore
{
    private DbSet<PublicEvidenceCacheEntry> Entries => db.Set<PublicEvidenceCacheEntry>();
    public async Task<PublicEvidenceDocument?> FindAsync(string key, DateTimeOffset now, CancellationToken ct)
    {
        Key(key);
        var row = await Entries.AsNoTracking().SingleOrDefaultAsync(e => e.Key == key && e.PayloadJson != null && e.ExpiresAtUtc > now, ct);
        if (row is null) return null;
        if (row.Provider is null || row.Origin is null || row.Version is null || row.ContentSha256 is null || row.AcquiredAtUtc is null)
            throw new GenerationStoppedException("public_evidence_invalid");
        return new(row.Key, row.Kind, row.Provider, row.Origin.Value, row.Version, row.PayloadJson!, row.ContentSha256,
            row.AcquiredAtUtc.Value, row.ExpiresAtUtc);
    }
    public async Task<PublicEvidenceLease?> ClaimAsync(string key, PublicEvidenceKind kind, DateTimeOffset now, TimeSpan duration, CancellationToken ct)
    {
        Transaction(); Key(key);
        if (!Enum.IsDefined(kind) || duration < TimeSpan.FromSeconds(15) || duration > TimeSpan.FromMinutes(2)) throw new ArgumentException("public_evidence_lease_invalid");
        var row = await LockAsync(key, ct);
        if (row is not null && ((row.LeaseToken.HasValue && row.LeaseExpiresAtUtc > now) || (row.PayloadJson is not null && row.ExpiresAtUtc > now))) return null;
        if (row is null) { row = new() { Key = key }; Entries.Add(row); }
        row.Kind = kind; row.Provider = null; row.Origin = null; row.Version = null; row.PayloadJson = null;
        row.ContentSha256 = null; row.AcquiredAtUtc = null;
        row.LeaseToken = Guid.NewGuid(); row.LeaseExpiresAtUtc = now.Add(duration);
        row.ExpiresAtUtc = now.Add(duration).AddMinutes(2); row.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct); return new(key, row.LeaseToken.Value, row.LeaseExpiresAtUtc.Value);
    }
    public async Task PublishAsync(PublicEvidenceLease lease, PublicEvidenceDocument document, DateTimeOffset now, long maximumBytes, int maximumEntries, CancellationToken ct)
    {
        Transaction(); Key(document.Key);
        long bytes = Encoding.UTF8.GetByteCount(document.PayloadJson);
        if (lease.Key != document.Key || document.Origin is not (EvidenceOrigin.Live or EvidenceOrigin.Synthetic) ||
            document.Provider.Length is < 1 or > 32 || document.Version.Length is < 1 or > 64 || bytes is < 2 or > 2_097_152 ||
            document.ExpiresAtUtc <= now || document.ExpiresAtUtc > now.AddDays(1) || document.AcquiredAtUtc > now ||
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(document.PayloadJson))) != document.ContentSha256 ||
            maximumBytes is < 1_048_576 or > 536_870_912 || maximumEntries is < 10 or > 50_000)
            throw new ArgumentException("public_evidence_publication_invalid");
        var row = await LockAsync(lease.Key, ct) ?? throw new InvalidOperationException("public_evidence_lease_lost");
        Fence(row, lease, now);
        // Image bytes have their own quota and must never evict recommendation evidence.
        var partition = Entries.Where(e => document.Kind == PublicEvidenceKind.Artwork
            ? e.Kind == PublicEvidenceKind.Artwork : e.Kind != PublicEvidenceKind.Artwork);
        long total = await partition.SumAsync(e => e.StoredBytes, ct);
        int count = await partition.CountAsync(ct);
        if (total + bytes > maximumBytes || count > maximumEntries)
        {
            var victims = await partition.Where(e => e.Key != lease.Key && (e.LeaseToken == null || e.LeaseExpiresAtUtc <= now))
                .OrderBy(e => e.ExpiresAtUtc).ThenBy(e => e.UpdatedAtUtc).ThenBy(e => e.Key)
                .Select(e => new { e.Key, e.StoredBytes }).Take(100).ToArrayAsync(ct);
            var keys = new List<string>();
            foreach (var victim in victims)
            {
                if (total + bytes <= maximumBytes && count <= maximumEntries) break;
                keys.Add(victim.Key); total -= victim.StoredBytes; count--;
            }
            if (total + bytes > maximumBytes || count > maximumEntries) throw new GenerationStoppedException("public_evidence_quota");
            await Entries.Where(e => keys.Contains(e.Key)).ExecuteDeleteAsync(ct);
        }
        row.Kind = document.Kind; row.Provider = document.Provider; row.Origin = document.Origin; row.Version = document.Version;
        row.PayloadJson = document.PayloadJson; row.ContentSha256 = document.ContentSha256; row.AcquiredAtUtc = document.AcquiredAtUtc;
        row.ExpiresAtUtc = document.ExpiresAtUtc; row.UpdatedAtUtc = now; row.LeaseToken = null; row.LeaseExpiresAtUtc = null;
        await db.SaveChangesAsync(ct);
    }
    public async Task ReleaseAsync(PublicEvidenceLease lease, DateTimeOffset now, CancellationToken ct)
    {
        Transaction();
        await Entries.Where(e => e.Key == lease.Key && e.LeaseToken == lease.Token && e.PayloadJson == null).ExecuteDeleteAsync(ct);
    }
    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, int limit, CancellationToken ct)
    {
        Transaction(); if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var keys = await Entries.Where(e => e.ExpiresAtUtc <= now && (e.LeaseToken == null || e.LeaseExpiresAtUtc <= now))
            .OrderBy(e => e.ExpiresAtUtc).ThenBy(e => e.Key).Select(e => e.Key).Take(limit).ToArrayAsync(ct);
        return await Entries.Where(e => keys.Contains(e.Key)).ExecuteDeleteAsync(ct);
    }
    private async Task<PublicEvidenceCacheEntry?> LockAsync(string key, CancellationToken ct)
    {
        var row = await Entries.FromSqlInterpolated($"""SELECT * FROM "PublicEvidenceCache" WHERE "Key"={key} FOR UPDATE""").AsTracking().SingleOrDefaultAsync(ct);
        if (row is not null) await db.Entry(row).ReloadAsync(ct);
        return row;
    }
    private static void Fence(PublicEvidenceCacheEntry row, PublicEvidenceLease lease, DateTimeOffset now)
    {
        if (row.LeaseToken != lease.Token || row.LeaseExpiresAtUtc is not { } expiry || expiry <= now)
            throw new InvalidOperationException("public_evidence_lease_lost");
    }
    private static void Key(string key)
    {
        if (key.Length != 64 || key.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) throw new ArgumentException("public_evidence_key_invalid");
    }
    private void Transaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("public_evidence_write_requires_admission");
    }
}
