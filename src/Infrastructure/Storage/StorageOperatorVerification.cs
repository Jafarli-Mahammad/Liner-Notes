using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Infrastructure.Storage;

public sealed partial class LocalGenerationStorage
{
    /// <summary>Full stopped-writer verification without adopting artifacts or publishing inventory.</summary>
    public async Task<GenerationInventory> VerifyForOperatorAsync(GenerationStorageState database,
        string expectedPolicyFingerprint, CancellationToken ct = default)
    {
        if (commits is null) throw new GenerationStoppedException("storage_operator_admission_required");
        await using var ownership = OpenLease();
        var witness = await commits.ReadAsync(ct) ?? throw new GenerationStoppedException("storage_commit_witness_uninitialized");
        if (witness.Plan.RootFingerprint != expectedPolicyFingerprint)
            throw new GenerationStoppedException("storage_policy_changed");
        if (witness.ExpectedRevision != database.Revision)
            throw new GenerationStoppedException("database_inventory_changed");
        if (database.ReservedArtifactBytes != 0 || publications is not null && (await publications.ReadActiveAsync(ct)).Count != 0)
            throw new GenerationStoppedException("storage_pending_publications_require_recovery");
        var expected = JsonSerializer.Deserialize<Dictionary<string,ArtifactMeasurement>>(witness.Plan.ArtifactCategoriesJson)
            ?? throw new GenerationStoppedException("storage_commit_artifacts_invalid");
        var measured = await MeasureAsync(ct);
        if (expected.Count != measured.Count || measured.Any(p => !expected.TryGetValue(p.Key,out var value) || value != p.Value))
            throw new GenerationStoppedException("artifact_inventory_changed");
        var verified = new GenerationInventory(UtcNow,true,database.DatabaseBytes,database.Revision,measured,witness.CommitId);
        if (Total(verified) >= CapacityBytes) throw new GenerationStoppedException("storage_headroom");
        return verified;
    }
}
