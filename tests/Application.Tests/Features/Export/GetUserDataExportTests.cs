using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.DTOs.Export;
using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.Features.Export.Queries.GetUserDataExport;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
using NSubstitute;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Export;

public class GetUserDataExportTests
{
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
        var accountReader = Substitute.For<IAccountProfileReader>();
        var account = Profile(userId);
        accountReader.GetAsync(userId, Arg.Any<CancellationToken>()).Returns(account);

        userRepo.GetForExportAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        tasteRepo.GetForExportAsync(userId, Arg.Any<CancellationToken>()).Returns(new List<TasteSignal> { signal });
        digestRepo.GetExportPageAsync(userId, null, null, 100, Arg.Any<CancellationToken>()).Returns(new List<WeeklyDigest> { digest });

        var handler = new GetUserDataExportQueryHandler(userRepo, tasteRepo, digestRepo, accountReader, EmptyDiscovery());
        var result = await handler.Handle(new GetUserDataExportQuery(userId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("2.5", result.ExportVersion);
        Assert.Equal(account, result.Account);
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

        var handler = new GetUserDataExportQueryHandler(userRepo, tasteRepo, digestRepo,
            Substitute.For<IAccountProfileReader>(), EmptyDiscovery());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetUserDataExportQuery(userId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ExportsStoredIdentityValuesIncludingNulls()
    {
        var id = Guid.NewGuid();
        var account = Profile(id) with { Email = null, NormalizedEmail = null, PhoneNumber = null };
        var (handler, reader, _, _) = Setup(id);
        reader.GetAsync(id, Arg.Any<CancellationToken>()).Returns(account);
        var export = await handler.Handle(new(id), default);
        Assert.Equal(account, export.Account);
        Assert.Equal("domain@example.test", export.Subscriber.Email);
        Assert.Equal("<profile-markup>", export.Account!.UserName);
        Assert.Null(export.Account.Email);
        Assert.Null(export.Account.PhoneNumber);
    }

    [Fact]
    public async Task Handle_RejectsMissingIdentityProfile()
    {
        var id = Guid.NewGuid();
        var (handler, _, _, _) = Setup(id);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(id), default));
    }

    [Fact]
    public async Task Handle_PropagatesCancellation()
    {
        var id = Guid.NewGuid();
        var (handler, reader, _, _) = Setup(id);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        reader.GetAsync(id, cancellation.Token).Returns(Task.FromCanceled<AccountExportDto?>(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(new(id), cancellation.Token));
    }

    [Fact]
    public async Task Handle_PropagatesReadFailure()
    {
        var id = Guid.NewGuid();
        var (handler, reader, taste, _) = Setup(id);
        reader.GetAsync(id, Arg.Any<CancellationToken>()).Returns(Profile(id));
        taste.GetForExportAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<TasteSignal>>(new IOException("injected read failure")));
        await Assert.ThrowsAsync<IOException>(() => handler.Handle(new(id), default));
    }

    [Fact]
    public async Task Handle_LaterPageFailureDoesNotReturnPartialHistory()
    {
        var id = Guid.NewGuid();
        var (handler, reader, _, digests) = Setup(id);
        reader.GetAsync(id, Arg.Any<CancellationToken>()).Returns(Profile(id));
        digests.GetExportPageAsync(id, Arg.Any<DateTime?>(), Arg.Any<Guid?>(), 100, Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<DateTime?>(1) is null
                ? Task.FromResult(FullPage(id))
                : Task.FromException<IReadOnlyList<WeeklyDigest>>(new IOException("later-page failure")));
        await Assert.ThrowsAsync<IOException>(() => handler.Handle(new(id), default));
    }

    [Fact]
    public async Task Handle_LaterPageCancellationDoesNotReturnPartialHistory()
    {
        var id = Guid.NewGuid();
        var (handler, reader, _, digests) = Setup(id);
        reader.GetAsync(id, Arg.Any<CancellationToken>()).Returns(Profile(id));
        using var cancellation = new CancellationTokenSource();
        digests.GetExportPageAsync(id, Arg.Any<DateTime?>(), Arg.Any<Guid?>(), 100, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (call.ArgAt<DateTime?>(1) is null) return Task.FromResult(FullPage(id));
                cancellation.Cancel();
                return Task.FromCanceled<IReadOnlyList<WeeklyDigest>>(cancellation.Token);
            });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(new(id), cancellation.Token));
    }

    [Fact]
    public async Task Handle_ArchiveExceptionDoesNotReturnSuccessfulExport()
    {
        var id = Guid.NewGuid();
        var archive = Substitute.For<ILocalEmailArchive>();
        var (handler, reader, _, _) = Setup(id, archive);
        reader.GetAsync(id, Arg.Any<CancellationToken>()).Returns(Profile(id));
        archive.ReadAsync(id, Arg.Any<IReadOnlyList<WeeklyDigestDto>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<LocalEmailArchiveDto>(new IOException("archive read failure")));
        await Assert.ThrowsAsync<IOException>(() => handler.Handle(new(id), default));
    }

    [Fact]
    public async Task Handle_AllowsExplicitIncompleteArchiveWithoutHidingIssues()
    {
        var id = Guid.NewGuid();
        var archive = Substitute.For<ILocalEmailArchive>();
        var (handler, reader, _, _) = Setup(id, archive);
        reader.GetAsync(id, Arg.Any<CancellationToken>()).Returns(Profile(id));
        archive.ReadAsync(id, Arg.Any<IReadOnlyList<WeeklyDigestDto>>(), Arg.Any<CancellationToken>())
            .Returns(new LocalEmailArchiveDto("incomplete", [], ["copy_integrity_unavailable"]));
        var result = await handler.Handle(new(id), default);
        Assert.Equal("incomplete", result.LocalEmail!.Status);
        Assert.Equal(new[] { "copy_integrity_unavailable" }, result.LocalEmail.Issues);
    }

    private static IReadOnlyList<WeeklyDigest> FullPage(Guid id) => Enumerable.Range(0, 100)
        .Select(_ => new WeeklyDigest(id, new IsoWeek(2026, 42), DateTime.UtcNow, DateTime.UtcNow.AddDays(7)))
        .ToArray();

    private static AccountExportDto Profile(Guid id) => new(id, "<profile-markup>", "STORED_NORMALIZED",
        "identity@example.test", "IDENTITY@EXAMPLE.TEST", true, "+000555123", true,
        new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), id, null, null, null, null, false);

    private static (GetUserDataExportQueryHandler Handler, IAccountProfileReader Reader,
        ITasteSignalRepository Taste, IWeeklyDigestRepository Digests) Setup(Guid id, ILocalEmailArchive? archive = null,
            IDiscoveryProfileReader? discovery = null)
    {
        var users = Substitute.For<IUserRepository>();
        var taste = Substitute.For<ITasteSignalRepository>();
        var digests = Substitute.For<IWeeklyDigestRepository>();
        var reader = Substitute.For<IAccountProfileReader>();
        users.GetForExportAsync(id, Arg.Any<CancellationToken>()).Returns(new User("domain@example.test", id: id));
        taste.GetForExportAsync(id, Arg.Any<CancellationToken>()).Returns(Array.Empty<TasteSignal>());
        digests.GetExportPageAsync(id, null, null, 100, Arg.Any<CancellationToken>()).Returns(Array.Empty<WeeklyDigest>());
        return (new(users, taste, digests, reader, discovery ?? EmptyDiscovery(), archive), reader, taste, digests);
    }

    private static IDiscoveryProfileReader EmptyDiscovery()
    {
        var reader = Substitute.For<IDiscoveryProfileReader>();
        reader.GetForAccountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new DiscoveryExportDto(null, [], []));
        return reader;
    }

    [Fact]
    public async Task Handle_ExportsDiscoveryMetadataAndDoesNotHideItsReadFailure()
    {
        var id = Guid.NewGuid();
        var discovery = Substitute.For<IDiscoveryProfileReader>();
        var metadata = new DiscoveryExportDto(new(id, id, 7, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null), [], []);
        discovery.GetForAccountAsync(id, Arg.Any<CancellationToken>()).Returns(metadata);
        var (handler, account, _, _) = Setup(id, discovery: discovery);
        account.GetAsync(id, Arg.Any<CancellationToken>()).Returns(Profile(id));
        var exported = await handler.Handle(new(id), CancellationToken.None);
        Assert.Same(metadata, exported.Discovery);
        discovery.GetForAccountAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DiscoveryExportDto>(new IOException("discovery read failed")));
        await Assert.ThrowsAsync<IOException>(() => handler.Handle(new(id), CancellationToken.None));
    }
}
