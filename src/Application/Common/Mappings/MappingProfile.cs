using AutoMapper;
using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.DTOs.Export;
using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Application.DTOs.Taste;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;

namespace LinerNotes.Application.Common.Mappings;

/// <summary>
/// AutoMapper profile declaring object-to-object projections between Domain entities and Application DTOs.
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, SubscriberDto>();

        CreateMap<UserMusicConnection, UserMusicConnectionExportDto>()
            .ForCtorParam(nameof(UserMusicConnectionExportDto.ServiceType), opt => opt.MapFrom(src => src.ServiceType.ToString()));

        CreateMap<TasteSignal, TasteSignalDto>();

        CreateMap<Track, TrackDto>();

        CreateMap<MatchedTagContribution, MatchedTagContributionDto>();

        CreateMap<ScoreBreakdown, ScoreBreakdownDto>();

        CreateMap<WeeklyRecommendation, WeeklyRecommendationDto>()
            .ForCtorParam(nameof(WeeklyRecommendationDto.WhyThisPick), opt => opt.MapFrom(src => src.WhyThisPick()));

        CreateMap<WeeklyDigest, WeeklyDigestDto>()
            .ForCtorParam(nameof(WeeklyDigestDto.Week), opt => opt.MapFrom(src => src.Week.Value));
    }
}
