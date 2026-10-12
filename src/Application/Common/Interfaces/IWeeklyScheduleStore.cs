namespace LinerNotes.Application.Common.Interfaces;

// Keep the stored day text so invalid legacy values can be reported rather than normalized or invented.
public sealed record ScheduledUser(Guid Id, string DeliveryDay, int DeliveryHourUtc, DateTime? NextDigestAt);

public interface IScheduledTickLease : IAsyncDisposable
{
    Task EnsureHeldAsync(CancellationToken cancellationToken);
}

public interface IWeeklyScheduleStore
{
    Task<IScheduledTickLease?> TryAcquireTickAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ScheduledUser>> GetCandidatesAsync(IReadOnlyList<Guid> users, int limit, DateTime nowUtc, CancellationToken cancellationToken);
    Task<ScheduledUser?> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> InitializeAsync(ScheduledUser observed, DateTime nextUtc, DateTime nowUtc, CancellationToken cancellationToken);
    Task<bool> AdvanceAsync(ScheduledUser observed, DateTime nextUtc, DateTime nowUtc, CancellationToken cancellationToken);
}
