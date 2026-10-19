#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:project ../src/Infrastructure/Infrastructure.csproj
using System.Text.Json;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.Caching.Memory;

// Public catalog shape/coverage smoke only: no account history, database writes or recordings.
try
{
    if (args.Length != 7 || args[0] != "--approve-read-only-catalog" || args[1] != "--token-file" || args[3] != "--artist" || args[5] != "--tag")
        throw new ArgumentException();
    string tokenPath = Path.GetFullPath(args[2]);
    if ((File.GetAttributes(tokenPath) & FileAttributes.ReparsePoint) != 0 || new FileInfo(tokenPath).Length > 16000)
        throw new ArgumentException();
    if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(tokenPath) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0)
        throw new ArgumentException();
    string token = File.ReadLines(tokenPath).Single(line => line.StartsWith("ListenBrainz__Token=", StringComparison.Ordinal))["ListenBrainz__Token=".Length..];
    if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException();
    var options = new ListenBrainzOptions { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true, Token = token, MaximumRequests = 12 };
    using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(15) };
    using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
    var client = new ListenBrainzApiClient(http, options, new MetaBrainzRequestGate(options, TimeProvider.System), cache, TimeProvider.System);
    var provider = new ListenBrainzRecommendationProvider(client);
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(90));
    var seed = await provider.GetAsync(args[4], cancellation.Token);
    var artists = await provider.GetCandidatesByArtistsAsync([args[4]], 2, cancellation.Token);
    var tags = await provider.GetCandidatesByTagsAsync([args[6]], 2, cancellation.Token);
    var hydrated = await provider.HydrateCandidatesBatchAsync(artists.Concat(tags).ToArray(), cancellation.Token);
    var allGaps = seed.Gaps.Concat(artists.Gaps).Concat(tags.Gaps).Concat(hydrated.Gaps).ToArray();
    var gaps = allGaps.Select(g => g.Reason).Distinct().Order().ToArray();
    Console.WriteLine(JsonSerializer.Serialize(new { provider = provider.SourceName, artistCandidates = artists.Count, tagCandidates = tags.Count,
        hydratedCandidates = hydrated.Count, seedTagCount = seed.Tags.Count, seedVectorCount=seed.Vector.Count,
        tagExclusions=seed.Tags.Select(t=>t.ExclusionReason).Where(r=>r!=null).Distinct().ToArray(),gaps,
        gapMethods = allGaps.Select(g => new { method = g.Method, reason = g.Reason }).Distinct().ToArray(),
        rawResponsesSaved = false, accountHistoryRequested = false }));
    return hydrated.Count > 0 && !gaps.Any(g => g is "http_error" or "transport_error" or "rate_limit") ? 0 : 2;
}
catch { Console.WriteLine("{\"status\":\"stopped\",\"reason\":\"bounded_catalog_smoke_failed\"}"); return 2; }
