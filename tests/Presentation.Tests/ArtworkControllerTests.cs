using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Presentation.Discovery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class ArtworkControllerTests
{
    [Fact]
    public void KnownUnavailableCoverReturns404RatherThanASuccessfulPlaceholder()
    {
        var artwork = Substitute.For<IRecordingArtwork>();
        var id = Guid.NewGuid(); artwork.Request(id).Returns(RecordingArtworkState.Unavailable);
        var controller = new ArtworkController(artwork) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        Assert.Equal(404, Assert.IsType<StatusCodeResult>(controller.Read(id)).StatusCode);
        Assert.Equal("unavailable", controller.Response.Headers["X-Artwork-State"].ToString());
    }

    [Fact]
    public void PendingCoverAdvertisesTheActualNegativeCacheWindow()
    {
        var artwork = Substitute.For<IRecordingArtwork>();
        var id = Guid.NewGuid();
        artwork.RetryDelay(id).Returns(TimeSpan.FromSeconds(59.1));
        var controller = new ArtworkController(artwork) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        controller.Read(id);
        Assert.Equal("60", controller.Response.Headers.RetryAfter.ToString());
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadServesCachedCoverOrAnExplicitPendingStatusWithoutWarming(bool available)
    {
        var artwork = Substitute.For<IRecordingArtwork>();
        var id = Guid.NewGuid();
        byte[] bytes = [0xff, 0xd8, 0xff];
        artwork.GetCached(id).Returns(available ? new RecordingCover(bytes, "image/jpeg") : null);
        var controller = new ArtworkController(artwork) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var result = controller.Read(id);
        if (available)
        {
            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("image/jpeg", file.ContentType); Assert.Same(bytes, file.FileContents);
        }
        else Assert.Equal(503, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Equal(available ? "public,max-age=3600" : "no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal(available ? "ready" : "pending", controller.Response.Headers["X-Artwork-State"].ToString());
        artwork.DidNotReceive().WarmAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());

    }
}
