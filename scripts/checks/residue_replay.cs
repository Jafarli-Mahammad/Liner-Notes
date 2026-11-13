#:project ../../src/Application/Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
using System.Diagnostics;
using System.Text.Json;
using LinerNotes.Application.Common.Mappings;
using LinerNotes.Domain.Scoring;

if (args.Length != 2) throw new ArgumentException("Usage: residue_replay.cs SNAPSHOT PRIVATE_OUTPUT");
if (new FileInfo(args[0]).Length > 10_000_000) throw new ArgumentException("Snapshot exceeds 10 MB");
using var document = JsonDocument.Parse(File.ReadAllBytes(args[0]));
var scores = document.RootElement.GetProperty("picks").EnumerateArray().Select(p =>
{
    var id = p.TryGetProperty("Id", out var storedId) ? storedId : p.GetProperty("id");
    var encoded = p.TryGetProperty("ScoreBreakdown", out var storedScore) ? storedScore : p.GetProperty("scoreBreakdown");
    var score = encoded.TryGetProperty("snapshot", out _)
        ? JsonSerializer.Deserialize<ScoreBreakdown>(encoded.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
        : ScoreBreakdownJson.Deserialize(encoded.GetRawText());
    return (Id: id.GetGuid(), Score: score);
}).ToArray();
if (scores.Length == 0 || scores.Length > 500) throw new ArgumentException("Expected 1–500 preserved picks");
var scorer = new BaselineAScorer();
var comparisons = scores.Select(p =>
{
    var snapshot = p.Score.Snapshot ?? throw new ArgumentException("Stored pick has no replay snapshot");
    if (snapshot.Evidence.Selection is { } selection)
    {
        var result = LinerNotes.Domain.Discovery.ConnectedSelection.Replay(selection);
        if (!result.SelectedKeys.Contains(snapshot.TrackKey, StringComparer.Ordinal)) throw new InvalidOperationException("Stored pick is absent from selection replay");
    }
    var replay = scorer.Replay(snapshot);
    return new { p.Id, Error = Math.Abs(replay.FinalScore - p.Score.FinalScore),
        RawError = Math.Abs(replay.TagSimilarityRaw - p.Score.TagSimilarityRaw), Explanation = p.Score.GenerateExplanation() };
}).ToArray();
if (comparisons.Any(p => !double.IsFinite(p.Error) || !double.IsFinite(p.RawError) || p.Error > 1e-10 || p.RawError > 1e-10))
    throw new InvalidOperationException("Stored score differs from numerical replay");
// Fixed real receipts, warmed pure numerical replay only; excludes provider/database/UI costs.
for (int i = 0; i < 5; i++) foreach (var p in scores) scorer.Replay(p.Score.Snapshot!);
var samples = new List<double>();
long allocated = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 30; i++)
{
    var watch = Stopwatch.StartNew();
    foreach (var p in scores) scorer.Replay(p.Score.Snapshot!);
    samples.Add(watch.Elapsed.TotalMilliseconds);
}
allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
samples.Sort();
var result = new
{
    Version = "residue-numeric-replay-v1", Picks = scores.Length,
    SelectionReplayedPicks = scores.Count(p => p.Score.Snapshot!.Evidence.Selection is not null), MaxScoreError = comparisons.Max(p => p.Error),
    MaxRawSimilarityError = comparisons.Max(p => p.RawError),
    TagScopes = scores.GroupBy(p => p.Score.Snapshot!.Evidence.TagScope).ToDictionary(g => g.Key, g => g.Count()),
    ZeroTagOverlapPicks = scores.Count(p => p.Score.TagSimilarityRaw == 0),
    FamiliarArtistPicks = scores.Count(p => p.Score.Snapshot!.Familiarity),
    NumericalReplayBatchMedianMs = samples[15], NumericalReplayBatchP95Ms = samples[28],
    AllocatedBytesForThirtyReplays = allocated,
    Limit = "Warm pure numerical replay of fixed receipts; not job latency or subjective quality. Selection replay is checked separately where a receipt is present.",
    Explanations = comparisons
};
using (var output = new FileStream(args[1], FileMode.CreateNew))
{
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(args[1], UnixFileMode.UserRead | UnixFileMode.UserWrite);
    JsonSerializer.Serialize(output, result, new JsonSerializerOptions { WriteIndented = true });
}
Console.WriteLine(JsonSerializer.Serialize(new { result.Picks, result.MaxScoreError, result.TagScopes,
    result.ZeroTagOverlapPicks, result.FamiliarArtistPicks, result.NumericalReplayBatchMedianMs, result.NumericalReplayBatchP95Ms }));
