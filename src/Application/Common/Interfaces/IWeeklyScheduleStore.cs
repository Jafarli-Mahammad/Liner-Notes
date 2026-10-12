namespace LinerNotes.Application.Common.Interfaces;

// Keep the stored day text so invalid legacy values can be reported rather than normalized or invented.
public sealed record ScheduledUser(Guid Id, string DeliveryDay, int DeliveryHourUtc, DateTime? NextDigestAt);

public interface IScheduledTickLease : IAsyncDisposable
{
    /// <summary>Verifies that the scheduler lease is still held; fails if ownership has been lost.</summary>
    Task EnsureHeldAsync(CancellationToken cancellationToken);
}

public interface IWeeklyScheduleStore
{
    /// <summary>Attempts to acquire the scheduler lease without waiting for another tick; returns null when busy.</summary>
    /// <remarks>The caller must dispose an acquired lease.</remarks>
    Task<IScheduledTickLease?> TryAcquireTickAsync(CancellationToken cancellationToken);
    /// <summary>Returns at most <paramref name="limit"/> eligible listed accounts whose slots are due or missing.</summary>
    /// <remarks>Selection orders by least recent audit activity, due slot, then user ID; recording selection activity rotates bounded ticks. Deleted subscribers, deleted or missing Identity accounts, and unsubscribed accounts are excluded.</remarks>
    Task<IReadOnlyList<ScheduledUser>> GetCandidatesAsync(IReadOnlyList<Guid> users, int limit, DateTime nowUtc, CancellationToken cancellationToken);
    /// <summary>Returns the stored schedule, or null if the subscriber is missing, deleted, or unsubscribed, or its Identity account is missing or deleted.</summary>
    Task<ScheduledUser?> GetAsync(Guid userId, CancellationToken cancellationToken);
    /// <summary>Sets a missing slot if the account remains eligible and its delivery preferences still match <paramref name="observed"/>.</summary>
    /// <returns>Whether one row was updated, including its modification timestamp using <paramref name="nowUtc"/>.</returns>
    Task<bool> InitializeAsync(ScheduledUser observed, DateTime nextUtc, DateTime nowUtc, CancellationToken cancellationToken);
    /// <summary>Replaces the observed due slot if eligibility, preferences, and the stored due slot still match.</summary>
    /// <returns>Whether one row was updated, including its modification timestamp using <paramref name="nowUtc"/>.</returns>
    Task<bool> AdvanceAsync(ScheduledUser observed, DateTime nextUtc, DateTime nowUtc, CancellationToken cancellationToken);
}
