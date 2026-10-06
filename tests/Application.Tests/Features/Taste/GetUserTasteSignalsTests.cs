using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.Features.Taste.Queries.GetUserTasteSignals;
using LinerNotes.Domain.Taste;
using NSubstitute;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Taste;

public class GetUserTasteSignalsTests
{
    [Fact]
    public async Task Handle_ReturnsMappedSignals_ForUser()
    {
        var userId = Guid.NewGuid();
        var signal1 = TasteSignal.CreateSeedTag(userId, "krautrock", 0.8, "onboarding");
        var signal2 = TasteSignal.CreateSeedArtist(userId, "Can", 0.9, "onboarding");

        var tasteRepo = Substitute.For<ITasteSignalRepository>();
        tasteRepo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<TasteSignal> { signal1, signal2 });

        var handler = new GetUserTasteSignalsQueryHandler(tasteRepo);
        var result = await handler.Handle(new GetUserTasteSignalsQuery(userId), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("krautrock", result[0].TargetValue);
        Assert.Equal("Can", result[1].TargetValue);
    }
}
