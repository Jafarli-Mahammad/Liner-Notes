using LinerNotes.Application.DTOs.Export;
using MediatR;

namespace LinerNotes.Application.Features.Export.Queries.GetUserDataExport;

/// <summary>
/// Query generating a full JSON-exportable record of all data held for a subscriber.
/// </summary>
public record GetUserDataExportQuery(Guid UserId) : IRequest<UserDataExportDto>;
