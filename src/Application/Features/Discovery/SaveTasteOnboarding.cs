using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Taste;

namespace LinerNotes.Application.Features.Discovery;

public static class OnboardingState
{
    public static TasteOnboarding? Read(string? json) => json is null ? null : JsonSerializer.Deserialize<TasteOnboarding>(json);
}

public sealed record SaveOnboardingRequest(long Revision, TasteOnboarding Answers, DiscoveryTasteSelection Selection);

public interface ITasteOnboardingStore
{
    Task<long> SaveAsync(Guid profileId, long revision, TasteOnboarding answers,
        IReadOnlyList<TasteSignal> selections, DateTimeOffset now, CancellationToken ct);
}

public sealed class SaveTasteOnboarding(SelectDiscoveryTaste selections, IDiscoveryBatchStore profiles,
    ITasteOnboardingStore store, IDiscoveryWriteCoordinator admission, TimeProvider clock)
{
    public async Task<long> SaveAsync(Guid profileId, SaveOnboardingRequest request, CancellationToken ct)
    {
        request.Answers.Validate();
        var inputs = await profiles.ReadInputsAsync(profileId, clock.GetUtcNow(), 1, ct) ?? throw new UnauthorizedAccessException();
        var signals = await selections.ResolveAsync(profileId, request.Selection, ct, inputs.Signals);
        long bytes = checked(262_144L + JsonSerializer.SerializeToUtf8Bytes(request).LongLength * 4);
        return await admission.ExecuteAsync(bytes, token => store.SaveAsync(profileId, request.Revision,
            request.Answers, signals, clock.GetUtcNow(), token), ct);
    }
}
