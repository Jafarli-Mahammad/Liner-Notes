using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Infrastructure.Email;

public sealed partial class LocalEmailArchive
{
    private async Task<LocalEmailCleanupResult> DeleteManagedAsync(Guid userId, CancellationToken ct)
    {
        if (!options.Enabled) return new(false, ["archive_unavailable"]);
        if (publisher is null || publications is null || coordinator is null)
            return new(false, ["copy_cleanup_unavailable"]);
        var issues = new List<string>();
        try
        {
            string root = LocalEmailPaths.Root(options, storage);
            Guid? after = null;
            while (true)
            {
                var page = await publications.GetOwnerPageAsync(userId, after, 100, ct);
                foreach (var group in page.Where(operation => operation.Phase is
                    LinerNotes.Application.Common.Interfaces.Discovery.ArtifactPublicationPhase.Published or
                    LinerNotes.Application.Common.Interfaces.Discovery.ArtifactPublicationPhase.Deleting).Chunk(64))
                    await publisher.DeleteManyAsync(group, ct);
                foreach (var operation in page.Where(operation => operation.Phase is not
                    (LinerNotes.Application.Common.Interfaces.Discovery.ArtifactPublicationPhase.Published or
                     LinerNotes.Application.Common.Interfaces.Discovery.ArtifactPublicationPhase.Deleting)))
                    await publisher.DeleteAsync(operation, ct);
                if (page.Count < 100) break;
                if (after is not null && page[^1].Id.CompareTo(after.Value) <= 0)
                    throw new GenerationStoppedException("artifact_cleanup_cursor_invalid");
                after = page[^1].Id;
            }
            // Retain the old authenticated scan for legacy and unlinked copies. A legacy file
            // must already belong to the trusted inventory before a deletion intent is admitted.
            foreach (string file in Files(root, includePartials: true))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var bytes = await LocalEmailPaths.ReadAsync(file, options.StoredMessageByteLimit, ct);
                    var parsed = LocalEmailMessageSerializer.IsEncrypted(bytes)
                        ? serializer.ParseStoredCopy(bytes) : serializer.ParseLegacyStoredCopy(bytes);
                    if (parsed.Receipt.UserId != userId) continue;
                    var operation = await publisher.AdoptExistingAsync("copies", root, Path.GetFileName(file), userId, bytes, ct);
                    await publisher.DeleteAsync(operation, ct);
                }
                catch (Exception error) when (error is GenerationStoppedException or IOException or UnauthorizedAccessException)
                { issues.Add("copy_cleanup_integrity_unavailable"); }
            }
            await coordinator.ReclaimAsync(async token =>
            {
                await publications.DeleteTerminalOwnerRowsAsync(userId, token);
                return true;
            }, ct);
        }
        catch (Exception error) when (error is GenerationStoppedException or IOException or UnauthorizedAccessException or InvalidOperationException)
        { issues.Add("copy_cleanup_unavailable"); }
        return new(issues.Count == 0, issues.Distinct().ToArray());
    }
}
