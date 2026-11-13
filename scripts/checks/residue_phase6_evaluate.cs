#:project ../../src/Infrastructure/Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Scoring;
using LinerNotes.Infrastructure.Discovery;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.Caching.Memory;

if (args is ["--help"])
{
    Console.WriteLine("residue_phase6_evaluate.cs MANIFEST BASELINE_RUNNER_DLL --live; bounded public evidence only, no raw-response persistence or coefficient tuning");
    return;
}
if (args.Length != 3 || args[2] != "--live") throw new ArgumentException("Explicit --live and frozen manifest/baseline runner are required");
byte[] manifestBytes = File.ReadAllBytes(args[0]);
if (manifestBytes.Length > 100_000) throw new ArgumentException("manifest_size_limit");
var manifest = JsonSerializer.Deserialize<Manifest>(manifestBytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
if (manifest.Version != "residue-phase6-evaluation-v1" || manifest.Profiles.Length != 8 ||
    manifest.Profiles.Count(p => p.Fold == "development") != 4 || manifest.Profiles.Count(p => p.Fold == "held-out") != 4 ||
    manifest.GlobalDispatchCap is < 1 or > 80 || manifest.ProfileDispatchCap is < 1 or > 8 ||
    manifest.Settings.MaximumPicks != 5)
    throw new ArgumentException("invalid_evaluation_manifest");
manifest.Settings.Scoring();
var settingsHash = Hash(JsonSerializer.SerializeToUtf8Bytes(manifest.Settings));
var budget = new DiscoveryRequestBudget();
using var handler = new DispatchCounter(manifest.GlobalDispatchCap);
using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(8));
var ct = deadline.Token;
var results = new List<object>();
var acquisition = new List<object>();
foreach (var profile in manifest.Profiles)
{
    var watch = Stopwatch.StartNew();
    int start = handler.Dispatched;
    try
    {
        var pool = await Acquire(profile, manifest.Settings.Selection.AcquisitionLimit(profile.Answers, manifest.Settings.DiscoveryLimit), ct);
        var batch = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("phase6:" + profile.Id)).AsSpan(0, 16));
        var familiar = pool.FamiliarNames;
        var scoreConfig = manifest.Settings.Scoring();
        var scored = new BaselineAScorer().ScoreForBatch(pool.Candidates.Select(c => new BaselineACandidate(c.Track, c.TagVector,
            new([], [], c.Tags.Select(t => t.Store()).ToArray(), c.TagResponse.Store(), c.RawCandidate.Paths.Select(p => p.Store()).ToArray(),
                pool.Gaps.Select(g => g.Store()).ToArray(), c.TagScope, c.PopularityMissingReason))), pool.Taste, familiar, batch, scoreConfig).ToArray();
        var normalized = scored.Select(p =>
        {
            var c = pool.Candidates.Single(c => c.Track.TrackKey == p.Track.TrackKey);
            var best = ConnectedSelection.BestInterest(c.TagVector, pool.Interests, manifest.Settings.Selection.MinimumIndividualSharedTags);
            string? recording = c.RawCandidate.Paths.Any(path => path.Seed == profile.Recording && path.SimilarityResponse is not null) ? profile.Recording : null;
            var artists = c.CreditedArtistIds.Count == 0 ? new[] { "name:" + c.Track.NormalizedArtistName }
                : c.CreditedArtistIds.Distinct().Order().Select(id => "artist:" + id.ToString("D")).ToArray();
            return new SelectionCandidate(c.Track.TrackKey, TrackIdentity.Aliases(c.Track).Where(alias => alias != c.Track.TrackKey).ToArray(), artists,
                p.Breakdown.FinalScore, p.Breakdown.TagSimilarityRaw, best.Affinity, best.Identity, recording,
                p.Breakdown.NoveltyRaw, DiscoverySelectionPolicy.VocalMatch(p.Breakdown.Snapshot!.Evidence, "vocals"));
        }).ToArray();
        var input = new SelectionInput(manifest.Settings.Selection, profile.Answers, manifest.Settings.MaximumPicks, normalized, [], new Dictionary<string, int>());
        var receipt = ConnectedSelection.Select(input);
        ConnectedSelection.Replay(receipt);
        var baselineInput = new
        {
            BatchId = batch, Taste = pool.Taste.Weights, Answers = profile.Answers, Scoring = scoreConfig,
            Candidates = pool.Candidates.Select(c => new { c.Track.Title, c.Track.ArtistName, c.Track.Mbid, Tags = c.TagVector.Weights,
                Evidence = scored.Single(p => p.Track.TrackKey == c.Track.TrackKey).Breakdown.Snapshot!.Evidence,
                Familiar = familiar.Contains(c.Track.NormalizedArtistName) }).ToArray()
        };
        var baseline = await RunBaseline(JsonSerializer.Serialize(baselineInput), ct);
        var baselineScores = baseline.GetProperty("Scores").EnumerateArray().ToDictionary(x => x.GetProperty("TrackKey").GetString()!, StringComparer.Ordinal);
        double maximumError = scored.SelectMany(p => new[] {
            Math.Abs(p.Breakdown.FinalScore - baselineScores[p.Track.TrackKey].GetProperty("FinalScore").GetDouble()),
            Math.Abs(p.Breakdown.TagSimilarityRaw - baselineScores[p.Track.TrackKey].GetProperty("TagSimilarityRaw").GetDouble()),
            Math.Abs(p.Breakdown.NoveltyRaw - baselineScores[p.Track.TrackKey].GetProperty("NoveltyRaw").GetDouble())
        }).DefaultIfEmpty().Max();
        if (maximumError > 1e-10) throw new InvalidOperationException("paired_numerical_score_mismatch");
        for (int i = 0; i < 5; i++) ConnectedSelection.Select(input);
        var samples = new List<double>();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 30; i++)
        {
            var timer = Stopwatch.StartNew();
            new BaselineAScorer().ScoreForBatch(pool.Candidates.Select(c => new BaselineACandidate(c.Track, c.TagVector, ScoreEvidence.Empty)),
                pool.Taste, familiar, batch, scoreConfig).ToArray();
            ConnectedSelection.Select(input);
            samples.Add(timer.Elapsed.TotalMilliseconds);
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        samples.Sort();
        var legacy = baseline.GetProperty("SelectedKeys").EnumerateArray().Select(x => x.GetString()!).ToArray();
        results.Add(new { profile.Id, profile.Fold, Outcome = "evaluated", pool.RawCount, Hydrated = pool.Candidates.Count,
            TagScopes = pool.Candidates.GroupBy(c => c.TagScope).ToDictionary(g => g.Key, g => g.Count()),
            ActualDispatches = handler.Dispatched - start, AcquisitionMilliseconds = pool.AcquisitionMilliseconds,
            LegacySelectedKeys = legacy, IntegratedSelection = receipt, PairedScoreMaximumError = maximumError,
            CommonSelected = receipt.SelectedKeys.Intersect(legacy).Count(), LegacyPerformance = baseline,
            IntegratedPerformance = new { MedianMs = samples[15], P95Ms = samples[28], AllocatedPerRanking = allocated / 30 },
            pool.References, pool.Gaps, TotalMilliseconds = watch.Elapsed.TotalMilliseconds });
    }
    catch (Exception ex) when (ex is GenerationStoppedException or LinerNotes.Application.Common.Interfaces.Discovery.DiscoverySourceException or ArgumentException)
    { results.Add(new { profile.Id, profile.Fold, Outcome = "unavailable", Reason = ex.Message, Dispatches = handler.Dispatched - start }); }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { results.Add(new { profile.Id, profile.Fold, Outcome = "deadline" }); break; }
}
// One predeclared development and one held-out profile, equal cold-client dispatch allowances.
foreach (string id in new[] { "development-jazz", "heldout-pop" })
{
    var profile = manifest.Profiles.Single(p => p.Id == id);
    foreach (bool legacy in (profile.Fold == "development" ? new[] { true, false } : new[] { false, true }))
    {
        int start = handler.Dispatched;
        try
        {
            int limit = legacy ? manifest.Settings.DiscoveryLimit : manifest.Settings.Selection.AcquisitionLimit(profile.Answers, manifest.Settings.DiscoveryLimit);
            var pool = await Acquire(profile, limit, ct);
            acquisition.Add(new { profile.Id, profile.Fold, Policy = legacy ? "legacy-fixed-limit" : "coordinated-limit", Limit = limit,
                DispatchAllowance = manifest.ProfileDispatchCap, ActualDispatches = handler.Dispatched - start, pool.RawCount,
                Hydrated = pool.Candidates.Count, pool.AcquisitionMilliseconds, pool.References, pool.Gaps });
        }
        catch (Exception ex) when (ex is GenerationStoppedException or LinerNotes.Application.Common.Interfaces.Discovery.DiscoverySourceException or ArgumentException or OperationCanceledException)
        { acquisition.Add(new { profile.Id, Policy = legacy ? "legacy-fixed-limit" : "coordinated-limit", Outcome = "unavailable", Reason = ex.GetType().Name, Dispatches = handler.Dispatched - start }); }
    }
}
if (settingsHash != Hash(JsonSerializer.SerializeToUtf8Bytes(manifest.Settings)) || !manifestBytes.SequenceEqual(File.ReadAllBytes(args[0])))
    throw new InvalidOperationException("frozen_settings_changed");
Console.WriteLine(JsonSerializer.Serialize(new { Version = "residue-phase6-evaluation-v1", CapturedAtUtc = DateTimeOffset.UtcNow,
    ManifestSha256 = Hash(manifestBytes), SettingsSha256 = settingsHash, BaselineRunnerSha256 = Hash(File.ReadAllBytes(args[1])),
    BaselineCommit = manifest.BaselineCommit, IntegratedCommit = manifest.IntegratedCommit, Results = results,
    AcquisitionComparisons = acquisition, ActualTotalDispatches = handler.Dispatched, Dispatches = handler.Records,
    Limit = "Public independent examples. Same-pool selection comparison; acquisition-limit experiments use the current adapter with equal cold-client dispatch allowances, not an end-to-end historical pipeline. No listening-quality claim." }, new JsonSerializerOptions { WriteIndented = true }));

async Task<Pool> Acquire(Profile profile, int limit, CancellationToken token)
{
    using var allowance = budget.Begin(manifest.ProfileDispatchCap);
    using var cache = new MemoryCache(new MemoryCacheOptions());
    var options = new ListenBrainzOptions { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true, MaximumRequests = manifest.ProfileDispatchCap };
    var observed = new ObservingClient(new ListenBrainzApiClient(http, options, new MetaBrainzRequestGate(options, TimeProvider.System), cache, TimeProvider.System, budget: budget));
    var provider = new ListenBrainzRecommendationProvider(observed);
    var watch = Stopwatch.StartNew();
    var taste = profile.Tags.ToDictionary(t => t, _ => 1d, StringComparer.Ordinal);
    var interests = new List<SelectionInterest>();
    var gaps = new List<CoverageGap>();
    var familiar = new HashSet<string>(StringComparer.Ordinal);
    if (profile.Recording is { } recording)
    {
        var seed = await provider.GetRecordingAsync(Guid.Parse(recording), "Evaluation recording", token);
        DiscoverySourceValidation.ThrowIfFailed(seed.Gaps);
        gaps.AddRange(seed.Gaps);
        if (seed.MissingReason is { } reason) gaps.Add(new(recording, "seed.recording", reason, seed.Response));
        foreach (var tag in seed.Vector.Weights) taste[tag.Key] = taste.GetValueOrDefault(tag.Key) + tag.Value;
        interests.Add(new("recording:" + recording, seed.Vector, 1));
        var metadata = observed.Responses.FirstOrDefault(r => r.Reference.Method == "metadata.recording" && r.Reference.RequestIdentity == recording);
        if (metadata is not null && ListenBrainzEvidenceParser.Recording(metadata, recording) is { } credit) familiar.Add(credit.ArtistName.Trim().ToLowerInvariant());
    }
    var raw = new List<RawCandidateTrack>();
    if (profile.Recording is { } id)
    {
        var fromRecording = await provider.GetCandidatesByRecordingsAsync([Guid.Parse(id)], limit, token);
        raw.AddRange(fromRecording); gaps.AddRange(fromRecording.Gaps);
    }
    var fromTags = await provider.GetCandidatesByTagsAsync(profile.Tags, limit, token);
    raw.AddRange(fromTags); gaps.AddRange(fromTags.Gaps);
    var merged = raw.GroupBy(c => c.CandidateKey, StringComparer.Ordinal).Select(g => new RawCandidateTrack(g.First().Title, g.First().ArtistName,
        g.First().Mbid, g.SelectMany(c => c.Paths).Distinct())).ToArray();
    var hydrated = await provider.HydrateCandidatesBatchAsync(merged, token);
    gaps.AddRange(hydrated.Gaps);
    DiscoverySourceValidation.ThrowIfFailed(gaps.Concat(hydrated.SelectMany(c => c.Gaps)));
    return new(merged.Length, hydrated.Items, WeightedTagVector.FromDictionary(taste), interests, familiar,
        observed.Responses.Select(r => r.Reference).Distinct().ToArray(), gaps.ToArray(), watch.Elapsed.TotalMilliseconds);
}
async Task<JsonElement> RunBaseline(string input, CancellationToken token)
{
    var start = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    start.ArgumentList.Add(Path.GetFullPath(args[1]));
    using var process = Process.Start(start)!;
    await process.StandardInput.WriteAsync(input.AsMemory(), token); process.StandardInput.Close();
    var output = process.StandardOutput.ReadToEndAsync(token);
    var errors = process.StandardError.ReadToEndAsync(token);
    await process.WaitForExitAsync(token);
    if (process.ExitCode != 0) throw new InvalidOperationException("baseline_runner_failed:" + (await errors).Length);
    using var document = JsonDocument.Parse(await output);
    return document.RootElement.Clone();
}
static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
record Manifest(string Version, string BaselineCommit, string IntegratedCommit, GenerationConfiguration Settings, int GlobalDispatchCap, int ProfileDispatchCap, Profile[] Profiles);
record Profile(string Id, string Fold, string[] Tags, string? Recording, string Goal, string Exploration, string Vocals)
{ public TasteOnboarding Answers => new(Goal: Goal, Exploration: Exploration, Vocals: Vocals); }
record Pool(int RawCount, IReadOnlyList<HydratedCandidateTrack> Candidates, WeightedTagVector Taste, IReadOnlyList<SelectionInterest> Interests,
    IReadOnlySet<string> FamiliarNames, IReadOnlyList<ResponseReference> References, IReadOnlyList<CoverageGap> Gaps, double AcquisitionMilliseconds);
sealed class ObservingClient(IListenBrainzApiClient inner) : IListenBrainzApiClient
{
    public List<ListenBrainzResponse> Responses { get; } = [];
    public async Task<ListenBrainzResponse> SendAsync(ListenBrainzRequest request, CancellationToken ct = default)
    { var response = await inner.SendAsync(request, ct); Responses.Add(response); return response; }
}
sealed class DispatchCounter(int limit) : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.All })
{
    private int dispatched;
    public int Dispatched => dispatched;
    public List<object> Records { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        int previous;
        do { previous = dispatched; if (previous >= limit) throw new GenerationStoppedException("evaluation_dispatch_limit"); }
        while (Interlocked.CompareExchange(ref dispatched, previous + 1, previous) != previous);
        var watch = Stopwatch.StartNew();
        var response = await base.SendAsync(request, ct);
        Records.Add(new { Host = request.RequestUri!.Host, Path = request.RequestUri.AbsolutePath, Status = (int)response.StatusCode, Milliseconds = watch.Elapsed.TotalMilliseconds });
        return response;
    }
}
