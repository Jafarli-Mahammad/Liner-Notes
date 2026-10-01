using AutoMapper;
using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.DTOs.Export;
using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Application.DTOs.Taste;
using LinerNotes.Domain.Digest;
using MediatR;

namespace LinerNotes.Application.Features.Export.Queries.GetUserDataExport;

/// <summary>
/// Handler producing the comprehensive data export covering all stored tables and columns.
/// </summary>
public sealed class GetUserDataExportQueryHandler : IRequestHandler<GetUserDataExportQuery, UserDataExportDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ITasteSignalRepository _tasteSignalRepository;
    private readonly IWeeklyDigestRepository _weeklyDigestRepository;
    private readonly IMapper _mapper;

    public GetUserDataExportQueryHandler(
        IUserRepository userRepository,
        ITasteSignalRepository tasteSignalRepository,
        IWeeklyDigestRepository weeklyDigestRepository,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _tasteSignalRepository = tasteSignalRepository;
        _weeklyDigestRepository = weeklyDigestRepository;
        _mapper = mapper;
    }

    public async Task<UserDataExportDto> Handle(
        GetUserDataExportQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException(nameof(User), request.UserId);
        }

        var signals = await _tasteSignalRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var digests = await _weeklyDigestRepository.GetRecentDigestsForUserAsync(request.UserId, count: 1000, cancellationToken);

        var subscriberDto = _mapper.Map<SubscriberDto>(user);
        var connectionDtos = _mapper.Map<IReadOnlyList<UserMusicConnectionExportDto>>(user.Connections);
        var signalDtos = _mapper.Map<IReadOnlyList<TasteSignalDto>>(signals);
        var digestDtos = _mapper.Map<IReadOnlyList<WeeklyDigestDto>>(digests);

        return new UserDataExportDto(
            ExportVersion: "1.0",
            ExportedAtUtc: DateTime.UtcNow,
            Subscriber: subscriberDto,
            Connections: connectionDtos,
            TasteSignals: signalDtos,
            Digests: digestDtos);
    }
}
