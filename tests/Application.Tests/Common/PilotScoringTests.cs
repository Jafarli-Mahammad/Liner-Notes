using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using Xunit;

namespace LinerNotes.Application.Tests.Common;

public sealed class PilotScoringTests
{
    private static readonly IReadOnlySet<string> Empty = ImmutableHashSet<string>.Empty;
    private static WeightedTagVector Vector(params (string Tag, double Weight)[] entries) =>
        WeightedTagVector.FromDictionary(entries.ToDictionary(p => p.Tag, p => p.Weight));
    private static ResponseReference Reference(string seed, string method) =>
        new("Last.fm", method, seed, EvidenceOrigin.Synthetic, new string('a', 64), DateTimeOffset.UnixEpoch);
    internal static HydratedCandidateTrack Candidate(string title, string artist, WeightedTagVector vector,
        params (string Seed, double? Match)[] paths)
    {
        var raw = new RawCandidateTrack(title, artist, null, paths.Select(p => new DiscoveryPath(p.Seed,
            p.Match is { } m ? new(m, m.ToString(CultureInfo.InvariantCulture), null) : ObservedValue<double>.Missing(),
            Reference(p.Seed, "artist.getsimilar"), Reference(artist, "artist.gettoptracks"), 1, null, null, new(10, "10", null),
            SimilarityPosition: 1)));
        return new(Track.Create(title, artist), vector, raw, [], Reference(artist, "artist.gettoptags"), null);
    }
    private static EvaluationProfile Profile(params string[] seeds) => new("founder-1", EvaluationSet.Founder, "test", seeds);
    private static PilotIdf Corpus() => new(0, ImmutableSortedDictionary<string, int>.Empty, ImmutableSortedDictionary<string, double>.Empty, "hash");

    [Fact]
    public void AggregateAndBestSeedHaveIndependentAnalyticExpectations()
    {
        var seeds = new Dictionary<string, WeightedTagVector> { ["x"] = Vector(("rock", 1)), ["y"] = Vector(("jazz", 1)) };
        var candidate = Candidate("song", "artist", Vector(("rock", 1)), ("x", .4));
        var a = PilotScoring.Rank("A", Profile("x", "y"), seeds, [candidate], Corpus(), new(1, .2, 0), Empty, Empty).Single();
        var b = PilotScoring.Rank("B", Profile("x", "y"), seeds, [candidate], Corpus(), new(1, .2, 0), Empty, Empty).Single();
        Assert.Equal(1 / Math.Sqrt(2), a.TagScore, 12); Assert.Equal(1, b.TagScore);
        Assert.Equal("x", b.WinningSeed); Assert.Equal(.2, b.Novelty);
        Assert.Equal(b.Score, b.Contributions.Sum(c => c.Value) + b.Novelty + b.MatchBonus);
    }

    [Fact]
    public void MatchKeepsMissingAndRestrictsWinningEvidenceToProfileSeeds()
    {
        var seeds = new Dictionary<string, WeightedTagVector> { ["x"] = Vector(("rock", 1)) };
        var candidate = Candidate("song", "artist", Vector(("rock", 1)), ("x", .4), ("other-profile", .9));
        var score = PilotScoring.Rank("C", Profile("x"), seeds, [candidate], Corpus(), new(1, .2, .1), Empty, Empty).Single();
        Assert.Equal(.4, score.AggregatedMatch); Assert.Equal("x", score.WinningMatchPath!.Seed); Assert.Equal(.04, score.MatchBonus, 12);
        candidate = Candidate("song", "artist", Vector(("rock", 1)), ("x", null));
        score = PilotScoring.Rank("C", Profile("x"), seeds, [candidate], Corpus(), new(1, .2, .1), Empty, Empty).Single();
        Assert.Null(score.AggregatedMatch); Assert.Null(score.WinningMatchPath); Assert.Equal(0, score.MatchBonus);
    }

    [Fact]
    public void IdfCountsArtistsOnceAndExplainsSquareRootTransformation()
    {
        var artists = new[] { new PilotArtist("a", Vector(("common", 1), ("rare", 1)), [], Reference("a", "artist.gettoptags")),
            new PilotArtist("b", Vector(("common", 1)), [], Reference("b", "artist.gettoptags")),
            new PilotArtist("c", WeightedTagVector.Empty, [], Reference("c", "artist.gettoptags")) };
        var idf = PilotScoring.FitIdf(artists);
        Assert.Equal(2, idf.DocumentFrequency["common"]); Assert.Equal(1, idf.DocumentFrequency["rare"]);
        Assert.Equal(1 + Math.Log(2), idf.Factors["rare"]);
        var seeds = new Dictionary<string, WeightedTagVector> { ["x"] = Vector(("common", 1), ("rare", 1)) };
        var score = PilotScoring.Rank("D", Profile("x"), seeds,
            [Candidate("song", "artist", Vector(("rare", 1)), ("x", .5))], idf, new(1, .2, 0), Empty, Empty).Single();
        double expected = Math.Sqrt(idf.Factors["rare"] / (idf.Factors["rare"] + idf.Factors["common"]));
        Assert.Equal(expected, score.TagScore, 12); Assert.Equal(idf.Factors["rare"], score.Contributions.Single().Idf);
        Assert.Throws<ArgumentException>(() => PilotScoring.FitIdf([artists[0], artists[0]]));
    }

    [Fact]
    public void EmptyTagsZeroSubtotalAndKnownTrackExclusionDoNotSuppressSiblings()
    {
        var seeds = new Dictionary<string, WeightedTagVector> { ["x"] = WeightedTagVector.Empty };
        var a = Candidate("a", "artist", WeightedTagVector.Empty, ("x", null));
        var b = Candidate("b", "artist", WeightedTagVector.Empty, ("x", null));
        var result = PilotScoring.Rank("A", Profile("x"), seeds, [a, b], Corpus(), new(1, .2, 0),
            ImmutableHashSet.Create(a.RawCandidate.CandidateKey), Empty);
        Assert.Single(result); Assert.Equal(b.RawCandidate.CandidateKey, result[0].Key); Assert.Equal(0, result[0].TagScore);
        Assert.Equal("missing_seed_tag_vector", result[0].TagMissingReason); Assert.Empty(result[0].Contributions);
    }

    [Fact]
    public void AllOverlappingContributionsAreStoredAndNonfiniteInputsAreRejected()
    {
        var vector = Vector(Enumerable.Range(0, 12).Select(i => ($"tag{i}", 1d)).ToArray());
        var seeds = new Dictionary<string, WeightedTagVector> { ["x"] = vector };
        var candidates = new[] { Candidate("song", "artist", vector, ("x", .4)) };
        var score = PilotScoring.Rank("A", Profile("x"), seeds, candidates, Corpus(), new(1, .2, 0), Empty, Empty).Single();
        Assert.Equal(12, score.Contributions.Length); Assert.True(EvaluationMetrics.ExplanationValid(score.MetricPick()));
        Assert.DoesNotContain("TrackId", JsonSerializer.Serialize(score));
        Assert.Throws<ArgumentException>(() => PilotScoring.Rank("A", Profile("x"), seeds, candidates, Corpus(), new(double.NaN, .2, 0), Empty, Empty));
        seeds["x"] = Vector(("bad", double.NaN));
        Assert.Throws<ArgumentException>(() => PilotScoring.Rank("A", Profile("x"), seeds, candidates, Corpus(), new(1, .2, 0), Empty, Empty));
    }

    [Fact]
    public void HeldOutNeverEntersPilotAndPopularityCannotAlterRanking()
    {
        var seeds = new Dictionary<string, WeightedTagVector> { ["x"] = Vector(("rock", 1)) };
        var candidates = new[] { Candidate("a", "artist", seeds["x"], ("x", .5)) };
        Assert.Throws<ArgumentException>(() => PilotScoring.Rank("A", Profile("x") with { Set = EvaluationSet.HeldOut }, seeds, candidates, Corpus(), new(1, .2, 0), Empty, Empty));
        var original = PilotScoring.Rank("A", Profile("x"), seeds, candidates, Corpus(), new(1, .2, 0), Empty, Empty).Single();
        var raw = candidates[0].RawCandidate;
        var changed = new HydratedCandidateTrack(candidates[0].Track, candidates[0].TagVector,
            new(raw.Title, raw.ArtistName, null, raw.Paths.Select(p => p with { TrackListeners = new(1_000_000, "1000000", null) })),
            [], candidates[0].TagResponse, null);
        var after = PilotScoring.Rank("A", Profile("x"), seeds, [changed], Corpus(), new(1, .2, 0), Empty, Empty).Single();
        Assert.Equal(original.Score, after.Score); Assert.Equal(original.Key, after.Key);
    }

    [Fact]
    public void ConflictingOrImpreciseListenerCountsAreMissingAndInvalidMatchesCannotBecomeBonuses()
    {
        var candidate = Candidate("song", "artist", Vector(("rock", 1)), ("x", .4), ("y", .8));
        var raw = candidate.RawCandidate;
        var changed = new HydratedCandidateTrack(candidate.Track, candidate.TagVector,
            new(raw.Title, raw.ArtistName, null, raw.Paths.Select((p, i) => p with
            { TrackResponse = p.TrackResponse with { ResponseSha256 = new string(i == 0 ? 'a' : 'b', 64) }, TrackListeners = new(i + 1, (i + 1).ToString(), null) })),
            [], candidate.TagResponse, null);
        Assert.Null(PilotScoring.ComparableListeners(changed));
        changed = new(candidate.Track, candidate.TagVector,
            new(raw.Title, raw.ArtistName, null, raw.Paths.Select(p => p with { TrackListeners = new(long.MaxValue, "9223372036854775807", null) })),
            [], candidate.TagResponse, null);
        Assert.Null(PilotScoring.ComparableListeners(changed));
        var invalid = Candidate("song", "artist", Vector(("rock", 1)), ("x", double.NaN));
        Assert.Throws<ArgumentException>(() => PilotScoring.Rank("C", Profile("x"), new Dictionary<string, WeightedTagVector> { ["x"] = candidate.TagVector },
            [invalid], Corpus(), new(1, .2, .1), Empty, Empty));
    }

    [Fact]
    public void CanonicalBreakdownsMatchAcrossOneHundredShufflesAndFourCultures()
    {
        string? expected = null;
        var culture = CultureInfo.CurrentCulture;
        try
        {
            var random = new Random(947);
            foreach (string name in new[] { "", "en-US", "tr-TR", "az-Latn-AZ" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                for (int iteration = 0; iteration < 100; iteration++)
                {
                    var seedTags = new[] { ("indie", .3), ("rock", 1d), ("jazz", .5) }.OrderBy(_ => random.Next()).ToArray();
                    var seeds = new Dictionary<string, WeightedTagVector> { ["x"] = Vector(seedTags), ["y"] = Vector(("jazz", 1)) };
                    var candidates = new[] { Candidate("z", "ARTIST", Vector(seedTags.Reverse().ToArray()), ("y", .8), ("x", .8)),
                        Candidate("a", "artist", Vector(seedTags), ("x", .8), ("y", .8)) }.OrderBy(_ => random.Next()).ToArray();
                    var idf = PilotScoring.FitIdf([new("artist", Vector(seedTags), [], Reference("artist", "artist.gettoptags"))]);
                    string serialized = JsonSerializer.Serialize(new[] { "A", "B", "C", "D" }.Select(f =>
                        PilotScoring.Rank(f, Profile(iteration % 2 == 0 ? ["x", "y"] : ["y", "x"]), seeds, candidates,
                            idf, new(1, .2, f == "C" ? .1 : 0), Empty, Empty)));
                    expected ??= serialized; Assert.Equal(expected, serialized);
                }
            }
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}
