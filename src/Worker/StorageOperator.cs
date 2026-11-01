using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinerNotes.Worker;

public sealed record StorageOperatorResult(string Status, string? Reason = null, GenerationInventory? Inventory = null);

public static class StorageOperator
{
    public static async Task<StorageOperatorResult> ExecuteAsync(IServiceProvider services, string action,
        IConfiguration configuration, CancellationToken ct = default)
    {
        if (!configuration.GetValue<bool>("writers-stopped"))
            throw new GenerationStoppedException("storage_writers_stopped_acknowledgement_required");
        await services.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(ct);
        var store = services.GetRequiredService<IDigestGenerationStore>();
        await using var batch = await store.AcquireBatchAsync(ct);
        var storage = services.GetRequiredService<LocalGenerationStorage>();
        var options = services.GetRequiredService<GenerationStorageOptions>();
        var current = await store.ReadStorageAsync(ct);
        var commits = services.GetRequiredService<IManagedStorageCommitStore>();
        var witness = await commits.ReadAsync(ct);
        if (action == "establish-storage")
        {
            if (witness is not null) throw new GenerationStoppedException("storage_existing_witness_requires_verified_transition");
            if ((await services.GetRequiredService<IArtifactPublicationStore>().ReadActiveAsync(ct)).Count != 0)
                throw new GenerationStoppedException("storage_pending_publications_require_recovery");
            return new("established",Inventory:await storage.ReconcileAsync(current,ct));
        }
        var fingerprint = options.PolicyFingerprint();
        if (action == "change-storage-policy")
        {
            var previousSection = configuration.GetSection("StorageOperator:PreviousPolicy");
            var previous = previousSection.Get<GenerationStorageOptions>()
                ?? throw new GenerationStoppedException("storage_previous_policy_required");
            if (previousSection.GetValue<long?>("StopBytes") is null)
                throw new GenerationStoppedException("storage_previous_policy_required");
            // Configuration binding omits empty arrays; empty categories are legitimate in fixtures and imports.
            previous.ArtifactRoots = LocalGenerationStorage.Categories.ToDictionary(c => c,
                c => previousSection.GetSection("ArtifactRoots:" + c).Get<string[]>() ?? []);
            // Quota changes never move the namespace implicitly; restore uses identical in-container paths.
            if (previous.InventoryPath != options.InventoryPath || previous.LeasePath != options.LeasePath ||
                previous.PolicyFingerprint(legacy:true) != options.PolicyFingerprint(legacy:true))
                throw new GenerationStoppedException("storage_namespace_transition_forbidden");
            var legacy = configuration.GetValue<bool>("StorageOperator:LegacyPolicy");
            if (legacy && previous.StopBytes != LocalGenerationStorage.StopBytes)
                throw new GenerationStoppedException("storage_legacy_capacity_invalid");
            fingerprint = previous.PolicyFingerprint(legacy);
        }
        else if (action is not ("verify-storage" or "restore-storage"))
            throw new GenerationStoppedException("storage_explicit_operator_action_required");
        var verified = await storage.VerifyForOperatorAsync(current,fingerprint,ct);
        if (action is "change-storage-policy" or "restore-storage")
        {
            if (action == "restore-storage")
            {
                var path = configuration["StorageOperator:ExpectedInventoryFile"];
                if (!Path.IsPathFullyQualified(path ?? "")) throw new GenerationStoppedException("storage_restore_manifest_required");
                var info = new FileInfo(path!);
                if (!info.Exists || info.Length > 1_000_000) throw new GenerationStoppedException("storage_restore_manifest_invalid");
                var expected = JsonSerializer.Deserialize<GenerationInventory>(await File.ReadAllTextAsync(path!,ct));
                if (expected is null || expected.Categories is null || expected.CommitId != verified.CommitId || expected.DatabaseRevision != verified.DatabaseRevision ||
                    expected.Categories.Count != verified.Categories.Count || verified.Categories.Any(p =>
                        !expected.Categories.TryGetValue(p.Key,out var value) || value != p.Value))
                    throw new GenerationStoppedException("storage_restore_manifest_mismatch");
            }
            verified = await storage.ReconcileVerifiedAsync(current,verified.Categories,ct);
        }
        return new(action == "verify-storage" ? "verified" : "established",Inventory:verified);
    }
}
