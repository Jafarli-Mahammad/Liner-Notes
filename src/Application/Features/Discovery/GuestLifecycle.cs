using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.DTOs.Export;
using LinerNotes.Domain.Discovery;

namespace LinerNotes.Application.Features.Discovery;

public sealed class GuestLifecycle(IGuestProfileStore store, IDiscoveryWriteCoordinator admission, TimeProvider clock)
{
    public static readonly TimeSpan Inactivity = TimeSpan.FromDays(7);
    public static readonly TimeSpan ActivityInterval = TimeSpan.FromHours(1);

    public Task<ListenerProfile> StartAsync(string hash, CancellationToken ct) =>
        admission.ExecuteAsync(65_536, async token =>
            await store.FindGuestAsync(hash, clock.GetUtcNow(), token) ??
            await store.CreateAsync(hash, clock.GetUtcNow(), Inactivity, token), ct);

    public async Task<ListenerProfile?> ResolveGuestAsync(string hash, bool touch, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var profile = await store.FindGuestAsync(hash, now, ct);
        if (!touch || profile is null || now - profile.LastActiveAtUtc < ActivityInterval) return profile;
        try
        {
            await admission.ExecuteAsync(16_384, async token =>
            {
                await store.TouchAsync(profile.Id, now, Inactivity, ActivityInterval, token);
                return true;
            }, ct);
        }
        catch (GenerationStoppedException)
        {
            // Activity renewal must not hide saved results when managed writes stop.
            return profile;
        }
        return await store.FindGuestAsync(hash, now, ct);
    }

    public Task<DiscoveryExportDto> ExportAsync(Guid profileId, CancellationToken ct) => store.ExportAsync(profileId, ct);
    public Task ClearAsync(string hash, CancellationToken ct) => admission.ReclaimAsync(async token =>
    {
        await store.DeleteGuestAsync(hash, clock.GetUtcNow(), token);
        return true;
    }, ct);
    public Task ImportAsync(string hash, Guid userId, CancellationToken ct) => admission.ExecuteAsync(131_072, async token =>
    {
        await store.ImportIntoAccountAsync(hash, userId, clock.GetUtcNow(), token);
        return true;
    }, ct);
    public Task<int> CleanupAsync(int limit, CancellationToken ct) => admission.ReclaimAsync(
        token => store.DeleteExpiredAsync(clock.GetUtcNow(), limit, token), ct);
}
