using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Presentation.Discovery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class ArtworkControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadServesCachedCoverOrPlaceholderWithoutWarming(bool available)
    {
        var artwork = Substitute.For<IRecordingArtwork>();
        var id = Guid.NewGuid();
        byte[] bytes = [0xff, 0xd8, 0xff];
        artwork.GetCached(id).Returns(available ? new RecordingCover(bytes, "image/jpeg") : null);
        var controller = new ArtworkController(artwork) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var result = Assert.IsType<FileContentResult>(controller.Read(id));
        Assert.Equal(available ? "image/jpeg" : "image/svg+xml", result.ContentType);
        Assert.Equal(available ? "public,max-age=3600" : "no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal(available ? "ready" : "pending", controller.Response.Headers["X-Artwork-State"].ToString());
        artwork.DidNotReceive().WarmAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
        if (available) Assert.Same(bytes, result.FileContents);
    }
}
