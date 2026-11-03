#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:project ../../src/Application/Application.csproj
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;

if (args.Length != 1 || !int.TryParse(args[0], out int count) || count is < 1 or > 1500)
    throw new ArgumentException("Usage: residue_favorite_probe.cs COUNT (1–1500), synthetic local evidence only");
Guid Id(string text) => new(SHA256.HashData(Encoding.UTF8.GetBytes("residue-favorite-probe-v1:" + text)).AsSpan(0, 16));
var now = DateTimeOffset.UtcNow;
var owner = Id("owner");
var signals = Enumerable.Range(0, count).Select(i =>
{
    var kind = new[] { CatalogKind.Artist, CatalogKind.Album, CatalogKind.Track }[i % 3];
    string scope = kind switch { CatalogKind.Artist => "artist", CatalogKind.Album => "release-group", _ => "recording" };
    var item = new CatalogItemDto(Id("record:" + i), Id("canonical:" + i), kind, $"Fixture {i:D4}", "Fixture credit", null, null, [],
        "fixture", "Synthetic fixture", Id("version"), CatalogOrigin.Synthetic, [new(Id("credit:" + i), "Fixture credit", 0)],
        "fixture", "fixture", [new("dimension " + i % 8, 1, "synthetic-" + scope)], now, now.AddDays(1), new string('a', 64));
    var signal = TasteSignal.ForProfile(owner, null, kind switch { CatalogKind.Artist => TasteTargetType.Artist,
        CatalogKind.Album => TasteTargetType.Album, _ => TasteTargetType.Track }, item.Name, 1, TasteSignalSource.InitialSeedManual, "Synthetic probe");
    signal.SetCatalogSelection(item.Id, item.CanonicalId, JsonSerializer.Serialize(new CatalogSelectionSnapshot("catalog-selection-v1", item)));
    return signal;
}).ToArray();
var builder = new MaterializeDiscoveryTaste(new Resolver(), new() { Origin = EvidenceOrigin.Synthetic }, new(), TimeProvider.System);
var first = await builder.BuildAsync(signals, false, default, round: 0);
var later = await builder.BuildAsync(signals, false, default, round: 1);
for (int i = 0; i < 5; i++) await builder.BuildAsync(signals, false, default);
var durations = new List<double>();
long allocated = GC.GetTotalAllocatedBytes(true);
for (int i = 0; i < 30; i++)
{
    var watch = Stopwatch.StartNew();
    await builder.BuildAsync(signals, false, default, round: i);
    durations.Add(watch.Elapsed.TotalMilliseconds);
}
allocated = GC.GetTotalAllocatedBytes(true) - allocated;
durations.Sort();
Console.WriteLine(JsonSerializer.Serialize(new { Version = "favorite-local-probe-v1", Favorites = count,
    ContributingSeeds = first.Seeds.Count, SummedTasteWeight = first.Vector.Weights.Values.Sum(),
    UnchangedAcrossRounds = first.Vector.Equals(later.Vector), MedianMs = durations[15], P95Ms = durations[28],
    AllocatedBytesForThirtyMaterializations = allocated,
    Limitation = "Synthetic saved local evidence, no HTTP/database work; behavioral and resource evidence, not listening quality." }));
sealed class Resolver : IRecommendationProviderResolver
{
    public RecommendationProvider Resolve() => new("fixture", null!, null!, new NoAcquisition());
}
sealed class NoAcquisition : ISeedTagSource
{
    public Task<SeedTagSnapshot> GetAsync(string name, CancellationToken ct = default)
        => throw new InvalidOperationException("Probe must not acquire provider data");
}
