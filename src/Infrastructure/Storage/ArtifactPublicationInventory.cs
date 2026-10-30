using System.Security.Cryptography;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Infrastructure.Storage;

public sealed partial class LocalGenerationStorage
{
    internal async Task<FileStream> AcquirePublicationNamespaceAsync(CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { return OpenLease(); }
            catch (GenerationStoppedException ex) when (ex.Message == "storage_lease_unavailable" && attempt < 100)
            { await Task.Delay(100, ct); }
        }
    }

    private sealed record PublicationLayout(IReadOnlySet<string>? Excluded, Dictionary<string, ArtifactPublication> Deletions);

    private async Task<PublicationLayout> PublicationLayoutAsync(IReadOnlySet<string>? explicitlyExcluded, CancellationToken ct)
    {
        if (publications is null) return new(explicitlyExcluded, new(StringComparer.Ordinal));
        var active = await publications.ReadActiveAsync(ct);
        if (active.Count > 64) throw new GenerationStoppedException("artifact_queue_invalid");
        var excluded = new HashSet<string>(explicitlyExcluded ?? new HashSet<string>(), StringComparer.Ordinal);
        var deletions = new Dictionary<string, ArtifactPublication>(StringComparer.Ordinal);
        foreach (var operation in active)
        {
            ValidatePublicationPaths(operation);
            string staging = Path.Combine(operation.Root, operation.StageName), final = Path.Combine(operation.Root, operation.FinalName);
            bool stageExists = File.Exists(staging), finalExists = File.Exists(final);
            if (operation.Phase == ArtifactPublicationPhase.Deleting)
            {
                if (stageExists || operation.LastWriteTimeUtcTicks <= 0) throw new GenerationStoppedException("artifact_deletion_invalid");
                deletions.Add(final, operation);
                continue;
            }
            if (operation.Phase == ArtifactPublicationPhase.Canceling)
            {
                if (stageExists) await VerifyPublicationContentAsync(staging, operation, ct);
                if (finalExists) await VerifyPublicationContentAsync(final, operation, ct);
                excluded.Add(staging);
                excluded.Add(final);
                continue;
            }
            // A final name is owned only after a durable publishing intent. Earlier creation is external.
            if (operation.Phase != ArtifactPublicationPhase.Publishing && finalExists)
                throw new GenerationStoppedException("artifact_unexpected_final");
            if (stageExists)
            {
                using (var candidate = DurableArtifactFile.OpenRead(staging))
                    if (candidate.Length > operation.ReservedBytes) throw new GenerationStoppedException("artifact_staging_invalid");
                if (operation.Phase != ArtifactPublicationPhase.Reserved) await VerifyPublicationContentAsync(staging, operation, ct);
            }
            if (operation.Phase != ArtifactPublicationPhase.Reserved && !stageExists && !finalExists)
                throw new GenerationStoppedException("artifact_publication_missing");
            if (operation.Phase == ArtifactPublicationPhase.Publishing)
            {
                if (stageExists && finalExists) throw new GenerationStoppedException("artifact_publication_conflict");
                if (finalExists) await VerifyPublicationContentAsync(final, operation, ct);
                excluded.Add(final);
            }
            excluded.Add(staging);
        }
        return new(excluded, deletions);
    }

    internal void ValidatePublicationPaths(ArtifactPublication operation)
    {
        static bool Name(string value) => value.Length is > 0 and <= 128 && value is not "." and not ".." &&
            value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
        if (!options.ArtifactRoots.TryGetValue(operation.Category, out var roots) ||
            !roots.Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))).Contains(operation.Root, StringComparer.Ordinal) || !Path.IsPathFullyQualified(operation.Root) ||
            !Name(operation.StageName) || !Name(operation.FinalName) || operation.StageName == operation.FinalName ||
            operation.ReservedBytes is < 1 or > 67_108_864 || operation.ContentBytes != operation.ReservedBytes ||
            operation.Id == Guid.Empty || operation.OwnerId == Guid.Empty || !Enum.IsDefined(operation.Phase) || operation.Version < 0 ||
            operation.LastWriteTimeUtcTicks < 0 || operation.LastWriteTimeUtcTicks > DateTime.MaxValue.Ticks ||
            operation.Sha256.Length != 64 || operation.Sha256.Any(c => !char.IsAsciiHexDigit(c)) ||
            operation.StageName != operation.Id.ToString("N") + ".staged")
            throw new GenerationStoppedException("artifact_publication_paths_invalid");
        for (string? path = operation.Root; path is not null; path = Path.GetDirectoryName(path))
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new GenerationStoppedException("artifact_publication_symlink");
    }

    internal static async Task VerifyPublicationContentAsync(string path, ArtifactPublication operation, CancellationToken ct)
    {
        if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new GenerationStoppedException("artifact_publication_symlink");
        using var verification = new ArtifactVerificationSession(useKernelReadLeases: false);
        byte[] actual = await verification.HashAsync(path, operation.ContentBytes, ct);
        if (operation.LastWriteTimeUtcTicks > 0 && File.GetLastWriteTimeUtc(path).Ticks != operation.LastWriteTimeUtcTicks)
            throw new GenerationStoppedException("artifact_publication_metadata_changed");
        if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(operation.Sha256)))
            throw new GenerationStoppedException("artifact_publication_checksum_invalid");
    }
}
