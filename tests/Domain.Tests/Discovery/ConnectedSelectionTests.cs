using System.Text.Json;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace LinerNotes.Domain.Tests.Discovery;

public sealed class ConnectedSelectionTests(ITestOutputHelper output)
{
    private static SelectionCandidate Candidate(string key, double affinity = 0.5, double novelty = 1,
        string? recording = null, double? score = null, double individual = 0, int vocals = 0, string? artist = null) =>
        new(key, [key], [artist ?? "artist:" + key], score ?? affinity + 0.2 * novelty, affinity, individual,
            individual > 0 ? "fixture-interest" : null, recording, novelty, vocals);
    private static SelectionInput Input(params SelectionCandidate[] candidates) =>
        new(new(), null, 5, candidates, [], new Dictionary<string, int>());

    [Fact]
    public void NoveltyAndLowOverlapCannotPadASparseBatchButRecordingRelationshipsRemainUsable()
    {
        var receipt = ConnectedSelection.Select(Input(Candidate("novel", 0), Candidate("weak", 0.01),
            Candidate("relevant"), Candidate("recording", 0, recording: Guid.NewGuid().ToString("D"))));
        Assert.Equal(2, receipt.SelectedKeys.Count);
        Assert.Equal(3, receipt.Shortfall);
        Assert.All(receipt.Decisions.Where(d => d.TrackKey is "novel" or "weak"), d => Assert.Equal("insufficient_connection", d.Reason));
        Assert.Contains("recording", receipt.SelectedKeys);
    }

    [Fact]
    public void ExclusionsPrecedePreferencesAndCanonicalCreditsControlDiversity()
    {
        var receipt = ConnectedSelection.Select(Input(Candidate("best", 1), Candidate("alias-a", 0.8, artist: "artist:shared"),
            Candidate("alias-b", 0.7, artist: "artist:shared"), Candidate("independent", 0.6)) with
        { ExcludedAliases = ["best"] });
        Assert.Equal(new[] { "alias-a", "independent" }, receipt.SelectedKeys);
        Assert.Equal("excluded", receipt.Decisions.Single(d => d.TrackKey == "best").Reason);
        Assert.Equal("artist_diversity", receipt.Decisions.Single(d => d.TrackKey == "alias-b").Reason);
    }

    [Fact]
    public void ControlsHaveJointEffectsWithoutUnconditionalGoalPrecedence()
    {
        var input = Input(Candidate("close", 0.85, 0), Candidate("favorite-path", 0.45, 0, Guid.NewGuid().ToString("D"), individual: 0.7)) with { Requested = 1 };
        var favorites = ConnectedSelection.Select(input with { Answers = new(Goal: "favorites", Exploration: "close") });
        var surprise = ConnectedSelection.Select(input with { Answers = new(Goal: "favorites", Exploration: "surprise") });
        Assert.Equal("close", Assert.Single(favorites.SelectedKeys));
        Assert.Equal("favorite-path", Assert.Single(surprise.SelectedKeys));
        Assert.NotEqual(surprise.Decisions.Single(d => d.TrackKey == "favorite-path").Rank,
            surprise.Decisions.Single(d => d.TrackKey == "favorite-path").RankWithoutExploration);
    }

    [Theory]
    [InlineData("vocals", "vocal")]
    [InlineData("instrumental", "instrumental")]
    public void SupportedVocalEvidenceCanChangeSelectionAndUnknownStaysNeutral(string preference, string expected)
    {
        var input = Input(Candidate("vocal", vocals: 1), Candidate("instrumental", vocals: -1), Candidate("unknown"))
            with
        { Requested = 1, Answers = new(Vocals: preference) };
        var receipt = ConnectedSelection.Select(input);
        Assert.Equal(expected, Assert.Single(receipt.SelectedKeys));
        Assert.Equal(0, receipt.Decisions.Single(d => d.TrackKey == "unknown").VocalAdjustment);
    }

    [Fact]
    public void RecentArtistConcentrationSoftensAndCanReverseWithDifferentHistory()
    {
        var input = Input(Candidate("a", 0.55), Candidate("b", 0.5)) with { Requested = 1 };
        Assert.Equal("a", Assert.Single(ConnectedSelection.Select(input).SelectedKeys));
        var penalized = ConnectedSelection.Select(input with { RecentArtists = new Dictionary<string, int> { ["artist:a"] = 3 } });
        Assert.Equal("b", Assert.Single(penalized.SelectedKeys));
        Assert.Equal("a", Assert.Single(ConnectedSelection.Select(input with { RecentArtists = new Dictionary<string, int> { ["artist:b"] = 3 } }).SelectedKeys));
        Assert.NotEqual(penalized.Decisions.Single(d => d.TrackKey == "b").Rank, penalized.Decisions.Single(d => d.TrackKey == "b").RankWithoutHistory);
    }

    [Fact]
    public void SelectionRoundTripsAndReplaysEveryDecisionAndRejectsTampering()
    {
        var input = Input(Candidate("a"), Candidate("duplicate", artist: "artist:a"), Candidate("empty", 0), Candidate("excluded"))
            with
        { ExcludedAliases = ["excluded"] };
        var receipt = ConnectedSelection.Select(input);
        var restored = JsonSerializer.Deserialize<SelectionReceipt>(JsonSerializer.Serialize(receipt))!;
        Assert.Equal(receipt.Decisions, ConnectedSelection.Replay(restored).Decisions);
        var compact = restored with { SelectedDecisionsOnly = true, Decisions = restored.Decisions.Where(d => d.Rank.HasValue).ToArray() };
        Assert.Equal(receipt.Decisions, ConnectedSelection.Replay(compact).Decisions);
        Assert.Throws<ArgumentException>(() => ConnectedSelection.Replay(compact with { SelectedDecisionsOnly = false }));
        Assert.Throws<ArgumentException>(() => ConnectedSelection.Replay(restored with { Shortfall = 0 }));
        Assert.Throws<ArgumentException>(() => ConnectedSelection.Replay(restored with { Decisions = restored.Decisions.Select(d => d with { Utility = d.Utility + 1 }).ToArray() }));
        Assert.Throws<NotSupportedException>(() => ConnectedSelection.Replay(restored with { Version = "future" }));
    }

    [Fact]
    public void IndividualAffinityRequiresMultipleSupportedDescriptorsAndRespectsInfluence()
    {
        var tags = WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["a"] = 1, ["b"] = 1 });
        Assert.Equal(0, ConnectedSelection.BestInterest(tags, [new("weak", WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["a"] = 1 }), 1)], 2).Affinity);
        Assert.Equal(0.4, ConnectedSelection.BestInterest(tags, [new("attenuated", tags, 0.4)], 2).Affinity, 12);
        Assert.Equal("a", ConnectedSelection.BestInterest(tags, [new("b", tags, 1), new("a", tags, 1)], 2).Identity);
    }

    [Fact]
    public void ValidPreferenceCombinationsStayBoundedDeterministicAndReplayable()
    {
        foreach (string? goal in new string?[] { null, "skipped", "new-artists", "favorites", "different-styles", "mixture" })
            foreach (string? exploration in new string?[] { null, "skipped", "close", "balance", "surprise" })
                foreach (string? vocals in new string?[] { null, "skipped", "vocals", "instrumental", "both", "no-preference" })
                    foreach (bool allStyles in new[] { false, true })
                    {
                        var input = Input(Candidate("a", vocals: 1), Candidate("b", 0.4, vocals: -1), Candidate("c", 0))
                            with
                        { Answers = new(Goal: goal, Exploration: exploration, Vocals: vocals, AllStyles: allStyles) };
                        var receipt = ConnectedSelection.Select(input);
                        Assert.Equal(receipt.Decisions, ConnectedSelection.Select(input with { Candidates = input.Candidates.Reverse().ToArray() }).Decisions);
                        Assert.Equal(receipt.SelectedKeys, ConnectedSelection.Replay(receipt).SelectedKeys);
                        Assert.DoesNotContain("c", receipt.SelectedKeys);
                    }
    }

    [Fact]
    public void AggregateBestSeedAndBoundedCombinationAreComparedOnIdenticalIndependentPools()
    {
        var fixtures = BenchmarkSeedFixtures.AllCandidates.ToArray();
        var comparisons = new List<object>();
        for (int index = 0; index < fixtures.Length; index += 2)
        {
            var primary = fixtures[index].TagVector;
            var secondary = fixtures[(index + 4) % fixtures.Length].TagVector;
            var aggregate = WeightedTagVector.FromDictionary(primary.Weights.Concat(secondary.Weights)
                .GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Sum(p => p.Value)));
            var rows = fixtures.Select(f =>
            {
                double combined = aggregate.CosineSimilarity(f.TagVector);
                double best = ConnectedSelection.BestInterest(f.TagVector, [new("primary", primary, 1), new("secondary", secondary, 1)], 2).Affinity;
                return new { f.Track.TrackKey, Aggregate = combined, Best = best, Bounded = combined + 0.35 * Math.Max(0, best - combined) };
            }).ToArray();
            comparisons.Add(new
            {
                Profile = index / 2,
                Aggregate = rows.OrderByDescending(r => r.Aggregate).Select(r => r.TrackKey),
                BestSeed = rows.OrderByDescending(r => r.Best).Select(r => r.TrackKey),
                Bounded = rows.OrderByDescending(r => r.Bounded).Select(r => r.TrackKey),
                Rows = rows
            });
            Assert.All(rows, r => Assert.InRange(r.Bounded - r.Aggregate, 0, 0.35));
        }
        output.WriteLine("AFFINITY_COMPARISON=" + JsonSerializer.Serialize(comparisons));
    }

    [Fact]
    public void MaximumNormalizedSelectionPoolHasBoundedReplayCost()
    {
        var input = Input(Enumerable.Range(0, 400).Select(i => Candidate("fixture-" + i.ToString("D3"), (i % 10) / 10.0,
            artist: "artist:" + (i % 100))).ToArray()) with
        { ExcludedAliases = Enumerable.Range(0, 40).Select(i => "fixture-" + i.ToString("D3")).ToArray() };
        for (int i = 0; i < 3; i++) ConnectedSelection.Select(input);
        var samples = new List<double>();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 30; i++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var receipt = ConnectedSelection.Select(input);
            samples.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Equal(400, receipt.Decisions.Count);
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        samples.Sort();
        output.WriteLine("SELECTION_PROBE=" + JsonSerializer.Serialize(new
        {
            Candidates = 400,
            Iterations = 30,
            MedianMs = samples[15],
            P95Ms = samples[28],
            AllocatedPerSelection = allocated / 30,
            Limit = "Warm normalized selection only; excludes affinity materialization, codec, SQL, providers and UI."
        }));
    }

    [Theory]
    [InlineData("close", 3)]
    [InlineData("balance", 6)]
    [InlineData("surprise", 10)]
    public void ExplorationControlsAcquisitionWithinTheConfiguredProviderCap(string exploration, int expected)
    {
        Assert.Equal(expected, new SelectionConfiguration().AcquisitionLimit(new(Exploration: exploration), 10));
        Assert.Equal(Math.Min(2, expected), new SelectionConfiguration().AcquisitionLimit(new(Exploration: exploration), 2));
    }

    [Fact]
    public void InvalidValuesAndOversizedReceiptsFailClosed()
    {
        Assert.Throws<ArgumentException>(() => ConnectedSelection.Select(Input(Candidate("bad", double.NaN))));
        Assert.Throws<ArgumentException>(() => ConnectedSelection.Select(Input(Candidate("a"), Candidate("a"))));
        Assert.Throws<ArgumentException>(() => ConnectedSelection.Select(Input() with { Configuration = new() { MinimumAffinity = 0 } }));
        Assert.Throws<ArgumentException>(() => ConnectedSelection.Select(Input() with { Candidates = Enumerable.Range(0, 401).Select(i => Candidate(i.ToString())).ToArray() }));
    }
}
