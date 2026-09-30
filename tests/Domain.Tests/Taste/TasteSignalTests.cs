using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
using Xunit;

namespace LinerNotes.Domain.Tests.Taste;

public sealed class TasteSignalTests
{
    [Fact]
    public void TasteSignal_PreservesProvenanceAndTransparencyMetadata()
    {
        var userId = Guid.NewGuid();
        var signal = TasteSignal.CreateSeedTag(
            userId: userId,
            tag: "atmospheric black metal",
            weight: 0.95,
            context: "Imported from Last.fm top artist scrobble tags");

        Assert.Equal(userId, signal.UserId);
        Assert.Equal(TasteTargetType.Tag, signal.TargetType);
        Assert.Equal("atmospheric black metal", signal.TargetValue);
        Assert.Equal("atmospheric black metal", signal.NormalizedTargetValue);
        Assert.Equal(0.95, signal.Weight);
        Assert.Equal(TasteSignalSource.InitialSeedManual, signal.Source);
        Assert.Equal("Imported from Last.fm top artist scrobble tags", signal.Context);
        Assert.True(signal.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void TasteSignal_FromDislike_RecordsNegativeAversionWithContext()
    {
        var userId = Guid.NewGuid();
        var signal = TasteSignal.CreateFromDislike(
            userId: userId,
            item: "Coldplay",
            type: TasteTargetType.Artist,
            context: "Disliked recommendation in week 2026-W39");

        Assert.Equal(userId, signal.UserId);
        Assert.Equal(TasteTargetType.Artist, signal.TargetType);
        Assert.Equal(-1.0, signal.Weight);
        Assert.Equal(TasteSignalSource.RecommendationDislike, signal.Source);
        Assert.Contains("week 2026-W39", signal.Context);
    }
}
