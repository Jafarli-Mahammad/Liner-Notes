using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace LinerNotes.Application.Common.Diagnostics;

/// <summary>Process-local diagnostics. Callers use fixed stage/cache names, never user or catalog identities.</summary>
public static class DiscoveryDiagnostics
{
    public const string MeterName = "LinerNotes.Discovery";
    private static readonly Meter Meter = new(MeterName);
    private static readonly ActivitySource Activities = new(MeterName);
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("discovery.stage.duration", "ms");
    private static readonly Counter<long> CacheReads = Meter.CreateCounter<long>("discovery.cache.reads");

    public static Measurement Measure(string stage) => new(stage, Duration.Enabled ? Stopwatch.GetTimestamp() : 0,
        Activities.StartActivity(stage, ActivityKind.Internal));
    public static void RecordDuration(string stage, TimeSpan elapsed) =>
        Duration.Record(elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("stage", stage));
    public static void CacheRead(string cache, bool hit) => CacheReads.Add(1,
        new KeyValuePair<string, object?>("cache", cache), new KeyValuePair<string, object?>("result", hit ? "hit" : "miss"));

    public readonly struct Measurement(string stage, long started, Activity? activity) : IDisposable
    {
        public void Dispose()
        {
            try { if (started != 0) RecordDuration(stage, Stopwatch.GetElapsedTime(started)); }
            finally { activity?.Dispose(); }
        }
    }
}
