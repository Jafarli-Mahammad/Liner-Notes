using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
using LinerNotes.Domain.Scoring;
using Xunit;
namespace LinerNotes.Domain.Tests;
public sealed class DiscoveryTasteMaterializerTests
{
    [Fact]
    public void ArtistAndRepeatedAlbumFallbackUseMaximumInfluenceWhileAlbumTagsRemainIndependent()
    {
        var profile = Guid.NewGuid();
        var artist = Signal(profile, TasteTargetType.Artist, "An Artist");
        var album1 = Signal(profile, TasteTargetType.Album, "An Album");
        var album2 = Signal(profile, TasteTargetType.Album, "Another Album");
        var tag = Signal(profile, TasteTargetType.Tag, "jazz");
        var vector = WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["jazz"] = 1 });
        EntityTasteEvidence[] evidence = [new(artist.Id, "artist:1", "artist", vector, 1, null),
            new(album1.Id, "artist:1", "artist", vector, .35, "album_tags_missing"),
            new(album2.Id, "artist:1", "artist", vector, .35, "album_tags_missing"),
            new(album2.Id, "release-group:2", "release-group", vector, 1, null)];
        TasteSignal[] signals = [artist, album1, album2, tag];
        var taste = DiscoveryTasteMaterializer.Build(signals, evidence);
        Assert.Equal(3, taste["jazz"]);
        Assert.Equal(taste, DiscoveryTasteMaterializer.Build(signals.Reverse().ToArray(), evidence.Reverse().ToArray()));
        Assert.Equal(.35, DiscoveryTasteMaterializer.Build([album1, album2], evidence[1..3])["jazz"]);
    }
    [Fact]
    public void ForeignEvidenceAndNonfiniteInfluenceAreRejected()
    {
        var signal = Signal(Guid.NewGuid(), TasteTargetType.Album, "Album");
        Assert.Throws<ArgumentException>(() => DiscoveryTasteMaterializer.Build([signal],
            [new(Guid.NewGuid(), "artist:1", "artist", WeightedTagVector.Empty, 1, null)]));
        Assert.Throws<ArgumentException>(() => DiscoveryTasteMaterializer.Build([signal],
            [new(signal.Id, "artist:1", "artist", WeightedTagVector.Empty, double.NaN, null)]));
    }
    [Fact]
    public void CanonicalAlbumScoreSnapshotsRetainMaterializationVersionAndReplay()
    {
        var vector = WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["ambient"] = 1 });
        var evidence = ScoreEvidence.Empty with { MaterializationVersion = DiscoveryTasteMaterializer.Version };
        var scorer = new BaselineAScorer();
        var result = scorer.ScoreForBatch(new(Track.Create("Synthetic track", "Synthetic artist"), vector, evidence),
            vector, false, Guid.NewGuid(), new());
        var snapshot = result.Snapshot!;
        Assert.Equal(DiscoveryTasteMaterializer.Version, snapshot.MaterializationVersion);
        Assert.Equal(result.FinalScore, scorer.Replay(snapshot).FinalScore);
        Assert.Equal(snapshot.MaterializationVersion, scorer.Replay(snapshot).Snapshot!.MaterializationVersion);
        Assert.Null(snapshot.Week);
    }
    private static TasteSignal Signal(Guid profile, TasteTargetType type, string value) =>
        TasteSignal.ForProfile(profile, null, type, value, 1, TasteSignalSource.InitialSeedManual, "Selected.");
}
