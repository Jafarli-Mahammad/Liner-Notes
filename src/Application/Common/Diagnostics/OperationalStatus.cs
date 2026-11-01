using System.Collections.Concurrent;

namespace LinerNotes.Application.Common.Diagnostics;

public enum OperationalComponent { Storage, Disk, Database, Acquisition, Prepared, Artwork, Maintenance, Recovery }
public sealed record OperationalObservation(bool Healthy, DateTimeOffset CheckedAt, DateTimeOffset? LastSuccess,
    DateTimeOffset? LastFailure, long Failures, string? FailureCode);
public sealed record OperationalQueue(int Pending,int Running,DateTimeOffset? OldestCreatedAt,DateTimeOffset? OldestExpiredLease);

/// <summary>Bounded, process-local operational state. No user, request or provider payload fields.</summary>
public sealed class OperationalStatus(TimeProvider clock)
{
    private readonly ConcurrentDictionary<OperationalComponent,OperationalObservation> observations = new();
    private OperationalQueue? queue;
    public OperationalQueue? Queue => Volatile.Read(ref queue);
    public void ReportQueue(OperationalQueue value) => Interlocked.Exchange(ref queue,value);
    public void Report(OperationalComponent component,bool healthy,string? code=null)
    {
        var now=clock.GetUtcNow();
        var safe=code is { Length: >0 and <=64 } && code.All(c=>c is >= 'a' and <= 'z' or '_') ? code : "operation_unavailable";
        observations.AddOrUpdate(component,_=>new(healthy,now,healthy?now:null,healthy?null:now,healthy?0:1,healthy?null:safe),
            (_,old)=>new(healthy,now,healthy?now:old.LastSuccess,healthy?old.LastFailure:now,
                old.Failures+(healthy?0:1),healthy?old.FailureCode:safe));
    }
    public IReadOnlyDictionary<OperationalComponent,OperationalObservation> Snapshot() => new Dictionary<OperationalComponent,OperationalObservation>(observations);
}
