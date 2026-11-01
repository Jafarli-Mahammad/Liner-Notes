using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.Storage;
using LinerNotes.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Npgsql;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task OperatorVerificationDoesNotPublishOrAdoptUnexplainedArtifacts()
    {
        var artifacts = Path.Combine(root,"recordings");
        Directory.CreateDirectory(artifacts);
        services.GetRequiredService<GenerationStorageOptions>().ArtifactRoots["recordings"] = [artifacts];
        await EnableDurableProtocolAsync();
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var before = (await sp.GetRequiredService<IManagedStorageCommitStore>().ReadAsync(default))!;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["writers-stopped"]="true" }).Build();
        var result = await StorageOperator.ExecuteAsync(sp,"verify-storage",config);
        Assert.Equal("verified",result.Status);
        Assert.Equal(before.CommitId,(await sp.GetRequiredService<IManagedStorageCommitStore>().ReadAsync(default))!.CommitId);
        var refused = await Assert.ThrowsAsync<GenerationStoppedException>(() => StorageOperator.ExecuteAsync(sp,"establish-storage",config));
        Assert.Equal("storage_existing_witness_requires_verified_transition",refused.Message);
        await File.WriteAllTextAsync(Path.Combine(artifacts,"unexplained.json"),"unexpected-fixture-content");
        var changed = await Assert.ThrowsAsync<GenerationStoppedException>(() => StorageOperator.ExecuteAsync(sp,"verify-storage",config));
        Assert.Equal("artifact_inventory_changed",changed.Message);
        Assert.Equal(before.CommitId,(await sp.GetRequiredService<IManagedStorageCommitStore>().ReadAsync(default))!.CommitId);
    }

    [LocalPostgresMigrationFact]
    public async Task ExplicitPolicyTransitionVerifiesOriginalWitnessBeforeChangingCapacity()
    {
        await EnableDurableProtocolAsync();
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var options = sp.GetRequiredService<GenerationStorageOptions>();
        var previous = System.Text.Json.JsonSerializer.Serialize(options);
        options.StopBytes = 2_000_000_000;
        var json = "{\"writers-stopped\":true,\"StorageOperator\":{\"PreviousPolicy\":"+previous+"}}";
        var config = new ConfigurationBuilder().AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))).Build();
        var changed = await StorageOperator.ExecuteAsync(sp,"change-storage-policy",config);
        Assert.Equal("established",changed.Status);
        Assert.Equal("verified",(await StorageOperator.ExecuteAsync(sp,"verify-storage",config)).Status);
        Assert.NotNull(await GuestAsync('f'));
    }

    [LocalPostgresMigrationFact]
    public async Task RestoreRejectsWrongManifestBeforeEstablishingAnyNewWitness()
    {
        await EnableDurableProtocolAsync();
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var before = (await sp.GetRequiredService<IManagedStorageCommitStore>().ReadAsync(default))!;
        var manifest = Path.Combine(root,"expected-inventory.json");
        await File.WriteAllTextAsync(manifest,"{}");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["writers-stopped"]="true",["StorageOperator:ExpectedInventoryFile"]=manifest }).Build();
        var error = await Assert.ThrowsAsync<GenerationStoppedException>(() => StorageOperator.ExecuteAsync(sp,"restore-storage",config));
        Assert.Equal("storage_restore_manifest_mismatch",error.Message);
        Assert.Equal(before.CommitId,(await sp.GetRequiredService<IManagedStorageCommitStore>().ReadAsync(default))!.CommitId);
    }

    [LocalPostgresMigrationFact]
    public async Task LegacyPolicyUpgradeRequiresMatchingLegacyWitnessAndPreservesData()
    {
        await EnableDurableProtocolAsync();
        var profile = await GuestAsync('e');
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var options = sp.GetRequiredService<GenerationStorageOptions>();
        var commits = sp.GetRequiredService<IManagedStorageCommitStore>();
        var before = (await commits.ReadAsync(default))!;
        await using (var batch = await sp.GetRequiredService<IDigestGenerationStore>().AcquireBatchAsync())
            await commits.EstablishAsync(Guid.NewGuid(),before.Plan with { RootFingerprint = options.PolicyFingerprint(legacy:true) },default);
        var json = "{\"writers-stopped\":true,\"StorageOperator\":{\"LegacyPolicy\":true,\"PreviousPolicy\":"+
            System.Text.Json.JsonSerializer.Serialize(options)+"}}";
        var config = new ConfigurationBuilder().AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))).Build();
        Assert.Equal("established",(await StorageOperator.ExecuteAsync(sp,"change-storage-policy",config)).Status);
        Assert.Equal("verified",(await StorageOperator.ExecuteAsync(sp,"verify-storage",config)).Status);
        Assert.Equal(profile.Id,(await sp.GetRequiredService<IGuestProfileStore>().FindGuestAsync(new string('e',64),DateTimeOffset.UtcNow,default))!.Id);
    }

    [LocalPostgresMigrationFact]
    public async Task OnlyVerifiedRestoreRebindsPhysicalIdentityWithMatchingPortableSchema()
    {
        await EnableDurableProtocolAsync();
        await using var scope=services.CreateAsyncScope();
        var sp=scope.ServiceProvider;
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["writers-stopped"]="true" }).Build();
        var source=await StorageOperator.ExecuteAsync(sp,"verify-storage",configuration);
        var expected=Path.Combine(root,"restore-proof.json");
        await File.WriteAllTextAsync(expected,System.Text.Json.JsonSerializer.Serialize(new StorageRestoreManifest(source.Inventory!,source.SchemaFingerprint!)));
        await using var owner=new NpgsqlConnection(connection); await owner.OpenAsync();
        string index,definition;
        await using(var query=new NpgsqlCommand("SELECT quote_ident(indexname),indexdef FROM pg_indexes WHERE tablename='ListenerProfiles' AND indexname NOT LIKE 'PK_%' LIMIT 1",owner))
        await using(var reader=await query.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync()); index=reader.GetString(0); definition=reader.GetString(1);
        }
        await using(var replace=new NpgsqlCommand("DROP INDEX public."+index+"; "+definition,owner)) await replace.ExecuteNonQueryAsync();
        Assert.Equal(source.SchemaFingerprint,await sp.GetRequiredService<IManagedStorageIntegrity>().ReadPortableSchemaFingerprintAsync(default));
        var refused=await Assert.ThrowsAsync<GenerationStoppedException>(()=>StorageOperator.ExecuteAsync(sp,"verify-storage",configuration));
        Assert.Equal("database_inventory_changed",refused.Message);
        var restoreConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["writers-stopped"]="true",["StorageOperator:ExpectedInventoryFile"]=expected }).Build();
        Assert.Equal("established",(await StorageOperator.ExecuteAsync(sp,"restore-storage",restoreConfig)).Status);
        Assert.NotNull(await GuestAsync('d'));
    }
}
