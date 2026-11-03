#:project ../../src/Domain/Domain.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Diagnostics;
using System.Text.Json;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Scoring;

if (args is ["--help"]) { Console.WriteLine("residue_phase6_performance.cs BASELINE_RUNNER_DLL; local synthetic pure scorer/selection measurements, no upstream calls"); return; }
if (args.Length != 1) throw new ArgumentException("Baseline runner DLL required");
var results = new List<object>();
foreach (int count in new[] { 10, 100, 400 })
{
    var tags = new Dictionary<string, double> { ["electronic"] = 1, ["ambient"] = .5 };
    var taste = WeightedTagVector.FromDictionary(tags);
    var candidates = Enumerable.Range(0, count).Select(i => new BaselineACandidate(
        Track.Create("Synthetic performance track " + i, "Synthetic artist " + i / 2), taste, ScoreEvidence.Empty)).ToArray();
    var scorer = new BaselineAScorer();
    var familiar = new HashSet<string>(StringComparer.Ordinal);
    var batch = Guid.Parse("00000000-0000-0000-0000-000000000006");
    var configuration = new BaselineAConfiguration(TagWeight: 1, NoveltyWeight: .2);
    var answers = new TasteOnboarding(Goal: "mixture", Exploration: "balance", Vocals: "no-preference");
    var scored = scorer.ScoreForBatch(candidates, taste, familiar, batch, configuration).ToArray();
    var selection = new SelectionInput(new(), answers, 5, scored.Select(p => new SelectionCandidate(
        p.Track.TrackKey, [], [p.Track.NormalizedArtistName], p.Breakdown.FinalScore, p.Breakdown.TagSimilarityRaw,
        0, null, null, p.Breakdown.NoveltyRaw, 0)).ToArray(), [], new Dictionary<string, int>());
    void Run() { scorer.ScoreForBatch(candidates, taste, familiar, batch, configuration).ToArray(); ConnectedSelection.Select(selection); }
    for (int i = 0; i < 5; i++) Run();
    var samples = new List<double>();
    long allocated = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 30; i++) { var timer = Stopwatch.StartNew(); Run(); samples.Add(timer.Elapsed.TotalMilliseconds); }
    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated; samples.Sort();
    var start = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    start.ArgumentList.Add(Path.GetFullPath(args[0]));
    using var process = Process.Start(start)!;
    await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { BatchId = batch, Taste = tags, Answers = answers, Scoring = configuration,
        Candidates = candidates.Select(c => new { c.Track.Title, c.Track.ArtistName, c.Track.Mbid, Tags = tags, Evidence = ScoreEvidence.Empty, Familiar = false }) }));
    process.StandardInput.Close();
    var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new InvalidOperationException("Baseline performance runner failed: " + (await error).Length);
    using var baseline = JsonDocument.Parse(await output);
    results.Add(new { Candidates = count, Baseline = new { MedianMs = baseline.RootElement.GetProperty("MedianMs").GetDouble(),
        P95Ms = baseline.RootElement.GetProperty("P95Ms").GetDouble(), AllocatedPerRanking = baseline.RootElement.GetProperty("AllocatedPerRanking").GetInt64() },
        Integrated = new { MedianMs = samples[15], P95Ms = samples[28], AllocatedPerRanking = allocated / 30 } });
}
Console.WriteLine(JsonSerializer.Serialize(new { Version = "residue-phase6-pure-performance-v1", Results = results,
    Limit = "Same prepared synthetic candidates, empty evidence, configuration, warmups and repetitions. Scoring plus final selection only; excludes input normalization, affinity preparation, codecs, IO, jobs and listening quality. Sequential local micro-measurement, not capacity or an end-to-end speedup claim." }, new JsonSerializerOptions { WriteIndented = true }));
