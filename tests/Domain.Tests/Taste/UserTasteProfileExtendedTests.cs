using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Taste;
using Xunit;

namespace LinerNotes.Domain.Tests.Taste;

public sealed class UserTasteProfileExtendedTests
{
    [Fact]
    public void UserTasteProfile_Constructor_EmptyUserId_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new UserTasteProfile(Guid.Empty, WeightedTagVector.Empty));
    }

    [Fact]
    public void UserTasteProfile_Constructor_NullTagPreferences_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new UserTasteProfile(Guid.NewGuid(), null!));
    }

    [Fact]
    public void UserTasteProfile_Lookups_ShouldBeCaseInsensitiveAndTrimmed()
    {
        var profile = new UserTasteProfile(
            userId: Guid.NewGuid(),
            tagPreferences: WeightedTagVector.Empty,
            familiarArtistNames: new HashSet<string> { "  Cocteau Twins  ", "The Cure" },
            familiarTrackKeys: new HashSet<string> { "mbid:track-123" },
            rejectedArtistNames: new HashSet<string> { "Coldplay" },
            rejectedTrackKeys: new HashSet<string> { "artist:bad song" });

        // Familiar artist check
        Assert.True(profile.IsArtistFamiliar("cocteau twins"));
        Assert.True(profile.IsArtistFamiliar("COCTEAU TWINS"));
        Assert.False(profile.IsArtistFamiliar("Slowdive"));
        Assert.False(profile.IsArtistFamiliar(""));

        // Familiar track check
        Assert.True(profile.IsTrackFamiliar("MBID:TRACK-123"));
        Assert.False(profile.IsTrackFamiliar("mbid:track-999"));
        Assert.False(profile.IsTrackFamiliar(""));

        // Rejected artist check
        Assert.True(profile.IsArtistRejected("coldplay"));
        Assert.False(profile.IsArtistRejected("Radiohead"));
        Assert.False(profile.IsArtistRejected("   "));

        // Rejected track check
        Assert.True(profile.IsTrackRejected("ARTIST:BAD SONG"));
        Assert.False(profile.IsTrackRejected("artist:good song"));
    }

    [Fact]
    public void UserTasteProfile_NullSets_ShouldDefaultToEmptySafely()
    {
        var profile = UserTasteProfile.Create(Guid.NewGuid(), WeightedTagVector.Empty);

        Assert.NotNull(profile.FamiliarArtistNames);
        Assert.NotNull(profile.FamiliarTrackKeys);
        Assert.NotNull(profile.RejectedArtistNames);
        Assert.NotNull(profile.RejectedTrackKeys);

        Assert.False(profile.IsArtistFamiliar("any"));
        Assert.False(profile.IsTrackFamiliar("any"));
        Assert.False(profile.IsArtistRejected("any"));
        Assert.False(profile.IsTrackRejected("any"));
    }
}
