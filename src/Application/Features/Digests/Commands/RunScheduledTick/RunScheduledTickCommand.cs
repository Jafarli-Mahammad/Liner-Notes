using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Digests.Commands.CaptureLocalDigest;
using LinerNotes.Application.Features.Digests.Commands.GenerateDigest;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using MediatR;

namespace LinerNotes.Application.Features.Digests.Commands.RunScheduledTick;

public sealed record RunScheduledTickCommand(IReadOnlyList<Guid> Users, int MaxUsersPerTick) : IRequest<ScheduleTickResult>;
public sealed record ScheduleJobResult(Guid UserId, DateTime? DueUtc, string? Week, string Status, string? Reason = null);
public sealed record ScheduleTickResult(string Status, IReadOnlyList<ScheduleJobResult> Jobs);

public sealed class RunScheduledTickCommandHandler(IWeeklyScheduleStore store, ISender sender, TimeProvider clock)
    : IRequestHandler<RunScheduledTickCommand, ScheduleTickResult>
{
    public async Task<ScheduleTickResult> Handle(RunScheduledTickCommand request, CancellationToken ct)
    {
        var users = request.Users.ToArray();
        if (users.Length is < 1 or > 100 || users.Any(id => id == Guid.Empty) || users.Distinct().Count() != users.Length ||
            request.MaxUsersPerTick is < 1 or > 100) throw new ArgumentException("invalid_schedule_request");
        ct.ThrowIfCancellationRequested();
        await using var lease = await store.TryAcquireTickAsync(ct).ConfigureAwait(false);
        if (lease is null) return new("scheduler_busy", []);
        var now = clock.GetUtcNow().UtcDateTime;
        var candidates = await store.GetCandidatesAsync(users, request.MaxUsersPerTick, now, ct).ConfigureAwait(false);
        var results = new List<ScheduleJobResult>();
        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            ScheduledUser? current = null;
            try
            {
                await lease.EnsureHeldAsync(ct).ConfigureAwait(false);
                current = await store.GetAsync(candidate.Id, ct).ConfigureAwait(false);
                if (current is null) { results.Add(new(candidate.Id, null, null, "inactive")); continue; }
                if (!Enum.TryParse<DigestDeliveryDay>(current.DeliveryDay, out var day) ||
                    !Enum.IsDefined(day) || day.ToString() != current.DeliveryDay)
                    throw new ArgumentException("invalid_schedule_state");
                var initialized = false;
                if (current.NextDigestAt is null)
                {
                    var next = WeeklySchedulePolicy.NextUtc(now, day, current.DeliveryHourUtc);
                    initialized = await store.InitializeAsync(current, next, now, ct).ConfigureAwait(false);
                    current = await store.GetAsync(candidate.Id, ct).ConfigureAwait(false);
                    if (current is null) { results.Add(new(candidate.Id, null, null, "inactive")); continue; }
                    if (current.NextDigestAt is null) throw new GenerationStoppedException("schedule_initialization_conflict");
                    // A concurrent preference edit must be validated using its new stored values.
                    if (!Enum.TryParse(current.DeliveryDay, out day) || !Enum.IsDefined(day) || day.ToString() != current.DeliveryDay)
                        throw new ArgumentException("invalid_schedule_state");
                }
                var due = current.NextDigestAt.Value;
                WeeklySchedulePolicy.ValidateSlot(due, day, current.DeliveryHourUtc);
                var week = WeeklySchedulePolicy.Week(due);
                if (due > now) { results.Add(new(current.Id, due, week.Value, initialized ? "initialized" : "future")); continue; }
                var generated = await sender.Send(new GenerateDigestCommand(current.Id, week), ct).ConfigureAwait(false);
                if (generated.Status is not ("generated" or "existing"))
                { results.Add(new(current.Id, due, week.Value, "stopped", generated.Reason ?? "generation_not_completed")); continue; }
                var captured = await sender.Send(new CaptureLocalDigestCommand(current.Id, week), ct).ConfigureAwait(false);
                if (captured.Status is not ("local_captured" or "already_captured"))
                { results.Add(new(current.Id, due, week.Value, "stopped", captured.Reason ?? "capture_not_completed")); continue; }
                ct.ThrowIfCancellationRequested();
                await lease.EnsureHeldAsync(ct).ConfigureAwait(false);
                var advanced = await store.AdvanceAsync(current, WeeklySchedulePolicy.Advance(due), now, ct).ConfigureAwait(false);
                results.Add(new(current.Id, due, week.Value, advanced ? "advanced" : "stopped",
                    advanced ? captured.Status : "schedule_changed_or_inactive"));
            }
            catch (OperationCanceledException) { throw; }
            catch (ArgumentException) { results.Add(new(candidate.Id, current?.NextDigestAt, null, "stopped", "invalid_schedule_state")); }
            catch (GenerationStoppedException ex) { results.Add(new(candidate.Id, current?.NextDigestAt, null, "stopped", ex.Message)); }
            catch (Exception) { results.Add(new(candidate.Id, current?.NextDigestAt, null, "stopped", "schedule_job_unavailable")); }
        }
        return new(results.Any(result => result.Status == "stopped") ? "stopped" : "completed", results);
    }
}
