using LinerNotes.Application.DTOs.Taste;
using MediatR;

namespace LinerNotes.Application.Features.Taste.Queries.GetUserTasteSignals;

/// <summary>
/// Query retrieving all stored taste signals and provenance data for a user.
/// </summary>
public record GetUserTasteSignalsQuery(Guid UserId) : IRequest<IReadOnlyList<TasteSignalDto>>;
