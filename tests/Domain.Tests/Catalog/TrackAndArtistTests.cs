using LinerNotes.Domain.Catalog;
using Xunit;

namespace LinerNotes.Domain.Tests.Catalog;

public sealed class TrackAndArtistTests
{
    [Fact]
    public void Artist_Create_ValidInputs_ShouldInitializeAndNormalize()
    {
        var artist = new Artist("  Slowdive  ", "mbid-slowdive-1");

        Assert.Equal("Slowdive", artist.Name);
        Assert.Equal("slowdive", artist.NormalizedName);
        Assert.Equal("mbid-slowdive-1", artist.Mbid);
        Assert.Equal("Slowdive", artist.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Artist_Create_EmptyName_ShouldThrowArgumentException(string? invalidName)
    {
        Assert.Throws<ArgumentException>(() => new Artist(invalidName!));
    }

    [Fact]
    public void Album_Create_ValidInputs_ShouldInitialize()
    {
        var album = new Album("Souvlaki", "Slowdive", "mbid-rel-1");

        Assert.Equal("Souvlaki", album.Title);
        Assert.Equal("Slowdive", album.ArtistName);
        Assert.Equal("mbid-rel-1", album.Mbid);
        Assert.Equal("Slowdive - Souvlaki", album.ToString());
    }

    [Theory]
    [InlineData("", "Artist")]
    [InlineData("Title", "")]
    [InlineData("   ", "   ")]
    public void Album_Create_EmptyTitleOrArtist_ShouldThrowArgumentException(string title, string artist)
    {
        Assert.Throws<ArgumentException>(() => new Album(title, artist));
    }

    [Fact]
    public void Track_TrackKey_WithMbid_ShouldPreferMbid()
    {
        var track = Track.Create("Alison", "Slowdive", "Souvlaki", "MBID-ALISON-123");

        Assert.Equal("mbid:mbid-alison-123", track.TrackKey);
    }

    [Fact]
    public void Track_TrackKey_WithoutMbid_ShouldUseNormalizedArtistAndTitle()
    {
        var track = Track.Create("  When the Sun Hits  ", "  Slowdive  ");

        Assert.Equal("slowdive:when the sun hits", track.TrackKey);
    }

    [Theory]
    [InlineData("", "Artist")]
    [InlineData("Song", "")]
    [InlineData(null, "Artist")]
    [InlineData("Song", null)]
    public void Track_Create_EmptyTitleOrArtist_ShouldThrowArgumentException(string? title, string? artist)
    {
        Assert.Throws<ArgumentException>(() => new Track(title!, artist!));
    }

    [Fact]
    public void Track_ExternalUrls_ShouldTrimAndAssign()
    {
        var track = new Track(
            "Song",
            "Artist",
            externalSpotifyUrl: " https://open.spotify.com/track/123 ",
            externalYoutubeUrl: " https://youtube.com/watch?v=abc ");

        Assert.Equal("https://open.spotify.com/track/123", track.ExternalSpotifyUrl);
        Assert.Equal("https://youtube.com/watch?v=abc", track.ExternalYoutubeUrl);
    }
}
