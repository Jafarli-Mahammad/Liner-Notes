#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:project ../../src/Domain/Domain.csproj
using System.Diagnostics;
using System.Text.Json;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;

var owner = Guid.NewGuid();
foreach (var size in new[] { 1000, 10000 })
{
    var rows = Enumerable.Range(0, size).Select(i => new FeedbackObservation(Guid.NewGuid(), owner, null,
        "Synthetic artist " + i % 40, "Recording " + i, null,
        i % 4 == 0 ? UserFeedback.Disliked : UserFeedback.Liked, null,
        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(i))).ToArray();
    for (int i = 0; i < 5; i++) EffectiveTasteInputs.FromProfile(owner, [], [], new(), rows);
    var durations = new List<double>();
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    for (var i = 0; i < 30; i++)
    {
        var watch = Stopwatch.StartNew();
        var result = EffectiveTasteInputs.FromProfile(owner, [], [], new(), rows);
        watch.Stop(); durations.Add(watch.Elapsed.TotalMilliseconds);
        if (result.FeedbackSupport.Count != 40) throw new InvalidOperationException("support mismatch");
    }
    var allocation = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 30;
    durations.Sort();
    Console.WriteLine(JsonSerializer.Serialize(new { size, medianMs = durations[15], p95Ms = durations[28], allocatedBytes = allocation,
        scope = "Compact synthetic feedback aggregation only; excludes database/provider I/O; no subjective evidence" }));
}
