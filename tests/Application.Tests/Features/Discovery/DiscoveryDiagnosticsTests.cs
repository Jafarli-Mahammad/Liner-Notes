using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using LinerNotes.Application.Common.Diagnostics;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Discovery;

public sealed class DiscoveryDiagnosticsTests
{
    [Fact]
    public async Task StageScopesKeepAsyncParentageAndPublishOnlyFixedLabelsOnFailure()
    {
        var metrics = new ConcurrentQueue<(double Value, KeyValuePair<string, object?>[] Tags)>();
        var activities = new ConcurrentQueue<Activity>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == DiscoveryDiagnostics.MeterName && instrument.Name == "discovery.stage.duration")
                owner.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
        {
            if (tags.ToArray().Any(tag => Equals(tag.Value, "diagnostics.contract"))) metrics.Enqueue((value, tags.ToArray()));
        });
        listener.Start();
        using var tracing = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DiscoveryDiagnostics.MeterName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { if (activity.OperationName.StartsWith("diagnostics.contract")) activities.Enqueue(activity); }
        };
        ActivitySource.AddActivityListener(tracing);
        var previous = Activity.Current;
        string? parent = null;
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var stage = DiscoveryDiagnostics.Measure("diagnostics.contract");
            parent = Activity.Current!.Id;
            await Task.Yield();
            using var child = DiscoveryDiagnostics.Measure("diagnostics.contract.child");
            Assert.Equal(parent, Activity.Current!.ParentId);
            throw new InvalidOperationException("private fixture input");
        });
        Assert.Same(previous, Activity.Current);
        var measured = Assert.Single(metrics);
        Assert.True(measured.Value >= 0);
        Assert.Equal("stage", Assert.Single(measured.Tags).Key);
        Assert.Equal(2, activities.Count);
        Assert.All(activities, activity => { Assert.Empty(activity.TagObjects); Assert.Empty(activity.Events); });
    }
}
