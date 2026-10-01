using AutoMapper;
using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.Common.Mappings;
using LinerNotes.Application.Features.Export.Queries.GetUserDataExport;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
using NSubstitute;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Export;

public class GetUserDataExportTests
{
    private readonly IMapper _mapper;

    public GetUserDataExportTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
    }

    [Fact]
    public async Task Handle_ReturnsCompleteTransparencyExport_WhenUserExists()
    {
        var userId = Guid.NewGuid();
        var user = new User("gdpr@example.com", "UTC", id: userId);
        var connection = new UserMusicConnection(userId, MusicServiceType.LastFm, "lastfm_user");
        user.AddConnection(connection);

        var signal = TasteSignal.CreateSeedTag(userId, "drone", 0.75, "manual");
        var digest = new WeeklyDigest(userId, new IsoWeek(2026, 39), DateTime.UtcNow.AddDays(-14), DateTime.UtcNow.AddDays(-7));

        var userRepo = Substitute.For<IUserRepository>();
        var tasteRepo = Substitute.For<ITasteSignalRepository>();
        var digestRepo = Substitute.For<IWeeklyDigestRepository>();

        userRepo.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        tasteRepo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(new List<TasteSignal> { signal });
        digestRepo.GetRecentDigestsForUserAsync(userId, 1000, Arg.Any<CancellationToken>()).Returns(new List<WeeklyDigest> { digest });

        var handler = new GetUserDataExportQueryHandler(userRepo, tasteRepo, digestRepo, _mapper);
        var result = await handler.Handle(new GetUserDataExportQuery(userId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("1.0", result.ExportVersion);
        Assert.Equal("gdpr@example.com", result.Subscriber.Email);
        Assert.Single(result.Connections);
        Assert.Equal("LastFm", result.Connections[0].ServiceType);
        Assert.Equal("lastfm_user", result.Connections[0].ExternalUsername);
        Assert.Single(result.TasteSignals);
        Assert.Equal("drone", result.TasteSignals[0].TargetValue);
        Assert.Single(result.Digests);
        Assert.Equal("2026-W39", result.Digests[0].Week);
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var userRepo = Substitute.For<IUserRepository>();
        var tasteRepo = Substitute.For<ITasteSignalRepository>();
        var digestRepo = Substitute.For<IWeeklyDigestRepository>();

        userRepo.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var handler = new GetUserDataExportQueryHandler(userRepo, tasteRepo, digestRepo, _mapper);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetUserDataExportQuery(userId), CancellationToken.None));
    }
}
