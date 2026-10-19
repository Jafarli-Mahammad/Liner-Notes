using System.Security.Cryptography;
using System.Text;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Discovery;
using Microsoft.Extensions.DependencyInjection;
namespace LinerNotes.Infrastructure.Discovery;

/// <summary>Cross-process public acquisition ownership. No provider call runs inside write admission.</summary>
public sealed class SharedPublicEvidence(IServiceScopeFactory scopes, GenerationConfiguration generation,
    DiscoveryRuntimeOptions options, TimeProvider clock)
{
    public async Task<PublicEvidenceDocument?> FindAsync(string key, PublicEvidenceKind kind, string version, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var document = await scope.ServiceProvider.GetRequiredService<IPublicEvidenceStore>().FindAsync(key, clock.GetUtcNow(), ct);
        if (document is null) return null;
        Validate(document, key, kind, version);
        return document;
    }
    public async Task<PublicEvidenceDocument> GetAsync(string key, PublicEvidenceKind kind, string version,
        bool allowAcquisition, Func<CancellationToken, Task<PublicEvidenceDocument>> acquire, CancellationToken ct)
    {
        options.Validate(); generation.Scoring();
        if (generation.Origin == EvidenceOrigin.Recorded) throw new GenerationStoppedException("evaluation_evidence_ineligible");
        var hit = await FindAsync(key, kind, version, ct);
        if (hit is not null) return hit;
        if (!allowAcquisition) throw new DiscoveryPreparationRequiredException();
        var lease = await WriteAsync(8192, (store, token) => store.ClaimAsync(key, kind, clock.GetUtcNow(), TimeSpan.FromSeconds(60), token), ct);
        if (lease is null) return await FindAsync(key, kind, version, ct) ?? throw new PublicPreparationBusyException();
        bool published = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(45));
            var document = await acquire(timeout.Token);
            Validate(document, key, kind, version);
            if (document.ExpiresAtUtc > clock.GetUtcNow())
            {
                long estimate = checked(Encoding.UTF8.GetByteCount(document.PayloadJson) * 4L + 65_536);
                await WriteAsync(estimate, async (store, token) => {
                    await store.PublishAsync(lease, document, clock.GetUtcNow(), options.PublicCacheBytes, options.PublicCacheEntries, token); return true;
                }, ct);
                published = true;
            }
            return document;
        }
        finally
        {
            if (!published)
            {
                // If cleanup is stopped, the bounded public lease expires and maintenance can remove it.
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await WriteAsync(8192, async (store, token) => { await store.ReleaseAsync(lease, clock.GetUtcNow(), token); return true; }, cleanup.Token); }
                catch (Exception ex) when (ex is GenerationStoppedException or OperationCanceledException) { }
            }
        }
    }
    private void Validate(PublicEvidenceDocument document, string key, PublicEvidenceKind kind, string version)
    {
        if (document.Key != key || document.Kind != kind || document.Version != version || document.Provider != generation.Provider ||
            document.Origin != generation.Origin || document.Origin == EvidenceOrigin.Recorded ||
            Encoding.UTF8.GetByteCount(document.PayloadJson) > 2_097_152 ||
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(document.PayloadJson))) != document.ContentSha256)
            throw new GenerationStoppedException("public_evidence_invalid");
    }
    private async Task<T> WriteAsync<T>(long bytes, Func<IPublicEvidenceStore, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPublicEvidenceStore>();
        return await scope.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(bytes, token => action(store, token), ct);
    }
}
