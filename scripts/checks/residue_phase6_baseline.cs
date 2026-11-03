#:project ../../src/Domain/Domain.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Diagnostics;
using System.Text.Json;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Scoring;

if (args is ["--help"])
{
    Console.WriteLine("Evaluation runner: compile with PHASE6_BASELINE against the frozen Phase 1 Domain project; read one bounded JSON input from stdin.");
    return;
}
#if PHASE6_BASELINE
string json = await Console.In.ReadToEndAsync();
if (json.Length > 4_000_000) throw new ArgumentException("evaluation_input_limit");
var input = JsonSerializer.Deserialize<EvaluationInput>(json)!;
if (input.Candidates.Length > 400) throw new ArgumentException("evaluation_candidate_limit");
var scorer = new BaselineAScorer();
var taste = WeightedTagVector.FromDictionary(input.Taste);
var candidates = input.Candidates.Select(c => new BaselineACandidate(Track.Create(c.Title, c.ArtistName, mbid: c.Mbid),
    WeightedTagVector.FromDictionary(c.Tags), c.Evidence)).ToArray();
var familiar = input.Candidates.Where(c => c.Familiar).Select(c => Track.Create(c.Title, c.ArtistName).NormalizedArtistName).ToHashSet(StringComparer.Ordinal);
BaselineAPick[] Run() => DiscoverySelectionPolicy.Rank(scorer.ScoreForBatch(candidates, taste, familiar, input.BatchId, input.Scoring), input.Answers, 5).ToArray();
for (int i = 0; i < 5; i++) Run();
var samples = new List<double>();
long allocated = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 30; i++) { var watch = Stopwatch.StartNew(); Run(); samples.Add(watch.Elapsed.TotalMilliseconds); }
allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
samples.Sort();
var scores = scorer.ScoreForBatch(candidates, taste, familiar, input.BatchId, input.Scoring).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new { SelectedKeys = Run().Select(p => p.Track.TrackKey).ToArray(),
    Scores = scores.Select(p => new { p.Track.TrackKey, p.Breakdown.FinalScore, p.Breakdown.TagSimilarityRaw, p.Breakdown.NoveltyRaw }),
    MedianMs = samples[15], P95Ms = samples[28], AllocatedPerRanking = allocated / 30 }));
#else
throw new InvalidOperationException("Compile this evaluation adapter with PHASE6_BASELINE against the frozen baseline project; see the Phase 6 run procedure.");
#endif

record EvaluationCandidate(string Title, string ArtistName, string? Mbid, Dictionary<string, double> Tags, ScoreEvidence Evidence, bool Familiar);
record EvaluationInput(Guid BatchId, Dictionary<string, double> Taste, EvaluationCandidate[] Candidates, TasteOnboarding Answers, BaselineAConfiguration Scoring);
