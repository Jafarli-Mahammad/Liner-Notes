using AutoMapper;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.DTOs.Taste;
using MediatR;

namespace LinerNotes.Application.Features.Taste.Queries.GetUserTasteSignals;

/// <summary>
/// Handler querying atomic taste signals for a specific subscriber.
/// </summary>
public sealed class GetUserTasteSignalsQueryHandler : IRequestHandler<GetUserTasteSignalsQuery, IReadOnlyList<TasteSignalDto>>
{
    private readonly ITasteSignalRepository _tasteSignalRepository;
    private readonly IMapper _mapper;

    public GetUserTasteSignalsQueryHandler(ITasteSignalRepository tasteSignalRepository, IMapper mapper)
    {
        _tasteSignalRepository = tasteSignalRepository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<TasteSignalDto>> Handle(
        GetUserTasteSignalsQuery request,
        CancellationToken cancellationToken)
    {
        var signals = await _tasteSignalRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        return _mapper.Map<IReadOnlyList<TasteSignalDto>>(signals);
    }
}
