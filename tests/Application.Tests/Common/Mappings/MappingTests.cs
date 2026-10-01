using AutoMapper;
using LinerNotes.Application.Common.Mappings;
using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.DTOs.Export;
using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Application.DTOs.Taste;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using Xunit;

namespace LinerNotes.Application.Tests.Common.Mappings;

public class MappingTests
{
    private readonly IMapper _mapper;

    public MappingTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        });

        config.AssertConfigurationIsValid();
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void AutoMapper_Configuration_IsValid()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        });

        config.AssertConfigurationIsValid();
    }

    [Fact]
    public void AutoMapper_Maps_User_To_SubscriberDto()
    {
        var user = new User("listener@example.com", "UTC", DigestDeliveryDay.Friday, 9);

        var dto = _mapper.Map<SubscriberDto>(user);

        Assert.Equal(user.Id, dto.Id);
        Assert.Equal("listener@example.com", dto.Email);
        Assert.Equal("UTC", dto.TimeZone);
        Assert.Equal(DigestDeliveryDay.Friday, dto.DeliveryDay);
        Assert.Equal(9, dto.DeliveryHourUtc);
    }

    [Fact]
    public void AutoMapper_Maps_TasteSignal_To_TasteSignalDto()
    {
        var signal = TasteSignal.CreateSeedTag(
            Guid.NewGuid(),
            "post-punk",
            0.85,
            "Manual user onboarding");

        var dto = _mapper.Map<TasteSignalDto>(signal);

        Assert.Equal(signal.Id, dto.Id);
        Assert.Equal(signal.UserId, dto.UserId);
        Assert.Equal(TasteTargetType.Tag, dto.TargetType);
        Assert.Equal("post-punk", dto.TargetValue);
        Assert.Equal("post-punk", dto.NormalizedTargetValue);
        Assert.Equal(0.85, dto.Weight);
        Assert.Equal(TasteSignalSource.InitialSeedManual, dto.Source);
    }

    [Fact]
    public void AutoMapper_Maps_WeeklyDigest_With_Nested_Recommendations_To_WeeklyDigestDto()
    {
        var startDate = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 10, 4, 23, 59, 59, DateTimeKind.Utc);
        var digest = new WeeklyDigest(Guid.NewGuid(), new IsoWeek(2026, 40), startDate, endDate);
        var track = new Track("Atmosphere", "Joy Division", "Closer");
        var breakdown = new ScoreBreakdown(
            0.82, 0.9, 0.9, 0.1, 0.05, 0.8, 0.08, 0.0, 0.0,
            new[] { new MatchedTagContribution("post-punk", 0.9, 0.8, 0.72) });

        digest.AddRecommendation(track, breakdown, 1);

        var dto = _mapper.Map<WeeklyDigestDto>(digest);

        Assert.Equal(digest.Id, dto.Id);
        Assert.Equal(digest.UserId, dto.UserId);
        Assert.Equal("2026-W40", dto.Week);
        Assert.Single(dto.Recommendations);

        var recDto = dto.Recommendations[0];
        Assert.Equal(1, recDto.Rank);
        Assert.Equal("Atmosphere", recDto.Track.Title);
        Assert.Equal("Joy Division", recDto.Track.ArtistName);
        Assert.Equal(0.82, recDto.ScoreBreakdown.FinalScore);
        Assert.Single(recDto.ScoreBreakdown.MatchedTags);
        Assert.Equal("post-punk", recDto.ScoreBreakdown.MatchedTags[0].TagName);
        Assert.Contains("Shared tags (post-punk)", recDto.WhyThisPick);
    }

    [Fact]
    public void MappingExtensions_ToDto_Performs_Equivalent_HighPerformance_Projection()
    {
        var startDate = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 10, 4, 23, 59, 59, DateTimeKind.Utc);
        var digest = new WeeklyDigest(Guid.NewGuid(), new IsoWeek(2026, 40), startDate, endDate);
        var track = new Track("Atmosphere", "Joy Division", "Closer");
        var breakdown = new ScoreBreakdown(
            0.82, 0.9, 0.9, 0.1, 0.05, 0.8, 0.08, 0.0, 0.0,
            new[] { new MatchedTagContribution("post-punk", 0.9, 0.8, 0.72) });

        digest.AddRecommendation(track, breakdown, 1);

        var dto = digest.ToDto();

        Assert.Equal(digest.Id, dto.Id);
        Assert.Equal("2026-W40", dto.Week);
        Assert.Single(dto.Recommendations);
        Assert.Equal("Atmosphere", dto.Recommendations[0].Track.Title);
        Assert.Equal(0.82, dto.Recommendations[0].ScoreBreakdown.FinalScore);
        Assert.Equal("post-punk", dto.Recommendations[0].ScoreBreakdown.MatchedTags[0].TagName);
    }
}
