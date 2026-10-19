using System.Security.Cryptography;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Domain.Catalog;

namespace LinerNotes.Application.Features.Catalog;

/// <summary>Verify the bounded artifact before writes; resume committed chunks and publish with one pointer switch.</summary>
public sealed class ImportCatalog(ICatalogImportStore store, ICatalogArtifactReader reader, IDiscoveryWriteCoordinator admission, TimeProvider clock)
{
    public async Task<Guid> RunAsync(CatalogPublication manifest, Stream artifact, bool exactManifestApproved,
        bool supplementaryUseApproved, CancellationToken ct)
    {
        if (!artifact.CanSeek || artifact.Length != manifest.ArtifactBytes || artifact.Length > 268_435_456)
            throw new ArgumentException("catalog_artifact_size_invalid");
        if (manifest.Origin == CatalogOrigin.Public && (!exactManifestApproved || manifest.MetadataLicense != "CC0-1.0" ||
            (manifest.TagLicense is not null && (!supplementaryUseApproved || manifest.TagLicense != "CC-BY-NC-SA-3.0"))))
            throw new InvalidOperationException("catalog_source_approval_required");
        if (manifest.Origin == CatalogOrigin.Synthetic && manifest.Source != "generated-fixture")
            throw new ArgumentException("catalog_synthetic_source_invalid");
        artifact.Position = 0;
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(artifact, ct));
        if (hash != manifest.ContentSha256) throw new ArgumentException("catalog_artifact_hash_mismatch");
        artifact.Position = 0;
        int count = 0;
        bool albumsStarted = false;
        await foreach (var row in reader.ReadAsync(artifact, manifest, ct))
        {
            if (row.Kind is CatalogKind.Album or CatalogKind.Track) albumsStarted = true;
            if (row.Kind == CatalogKind.Artist && albumsStarted) throw new ArgumentException("catalog_artists_must_precede_albums");
            if (++count > manifest.ExpectedRecords) throw new ArgumentException("catalog_artifact_count_mismatch");
        }
        if (count != manifest.ExpectedRecords) throw new ArgumentException("catalog_artifact_count_mismatch");
        var existing = await store.FindAsync(manifest.Id, ct);
        if (existing is null)
        {
            await admission.ExecuteAsync(65_536, async token => { await store.StartAsync(manifest, token); return true; }, ct);
            existing = manifest;
        }
        else if (existing.ContentSha256 != hash || existing.Source != manifest.Source || existing.SourceVersion != manifest.SourceVersion ||
            existing.SourceUri != manifest.SourceUri || existing.MetadataLicense != manifest.MetadataLicense || existing.TagLicense != manifest.TagLicense ||
            existing.Origin != manifest.Origin || existing.Attribution != manifest.Attribution || existing.ExpectedRecords != manifest.ExpectedRecords ||
            existing.AcquiredAtUtc != manifest.AcquiredAtUtc || existing.ExpiresAtUtc != manifest.ExpiresAtUtc || existing.IsLookup != manifest.IsLookup)
            throw new InvalidOperationException("catalog_manifest_identity_conflict");
        if (existing.State == CatalogPublicationState.Published) return existing.Id;
        if (existing.State != CatalogPublicationState.Staging) throw new InvalidOperationException("catalog_import_not_resumable");
        int cursor = existing.ImportedRecords, committedCursor = existing.ImportedRecords, processed = 0;
        artifact.Position = 0;
        var batch = new List<CatalogArtifactRow>(250);
        await foreach (var row in reader.ReadAsync(artifact, manifest, ct))
        {
            batch.Add(row);
            if (batch.Count == 250) await FlushAsync();
        }
        if (batch.Count > 0) await FlushAsync();
        await admission.ExecuteAsync(65_536, async token => { await store.PublishAsync(manifest.Id, clock.GetUtcNow(), token); return true; }, ct);
        return manifest.Id;

        async Task FlushAsync()
        {
            var rows = batch.ToArray();
            // Bounded worst-case JSON + normalized terms + relational/index overhead. Actual growth is checked at admission.
            long estimate = rows.Sum(r => (long)System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(r).Length * 6 + 16_384);
            if (processed + rows.Length <= committedCursor)
                await store.VerifyRowsAsync(manifest.Id, rows, ct);
            else
            {
                if (processed != cursor) throw new InvalidOperationException("catalog_resume_cursor_invalid");
                await admission.ExecuteAsync(estimate, async token => { await store.AppendAsync(manifest.Id, cursor, rows, token); return true; }, ct);
                cursor += rows.Length;
            }
            processed += rows.Length; batch.Clear();
        }
    }
}
