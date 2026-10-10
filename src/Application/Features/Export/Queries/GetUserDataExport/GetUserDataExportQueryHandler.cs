using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Domain.Enums;
using LinerNotes.Application.Common.Mappings;
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
/// Exports the reviewed taste/account records using bounded stable digest pages.
/// </summary>
public sealed class GetUserDataExportQueryHandler : IRequestHandler<GetUserDataExportQuery, UserDataExportDto>
{
    private readonly ILocalEmailArchive? _archive;
    private readonly IUserRepository _userRepository;
    private readonly ITasteSignalRepository _tasteSignalRepository;
    private readonly IWeeklyDigestRepository _weeklyDigestRepository;

    public GetUserDataExportQueryHandler(
        IUserRepository userRepository,
        ITasteSignalRepository tasteSignalRepository,
        IWeeklyDigestRepository weeklyDigestRepository, ILocalEmailArchive? archive = null)
    {
        _archive = archive;
        _userRepository = userRepository;
        _tasteSignalRepository = tasteSignalRepository;
        _weeklyDigestRepository = weeklyDigestRepository;
    }

    public async Task<UserDataExportDto> Handle(
        GetUserDataExportQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetForExportAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException(nameof(User), request.UserId);
        }

        var signals = await _tasteSignalRepository.GetForExportAsync(request.UserId, cancellationToken);
        var digests = new List<WeeklyDigestDto>();
        DateTime? beforeCreatedAt = null;
        Guid? beforeId = null;
        while (true)
        {
            var page = await _weeklyDigestRepository.GetExportPageAsync(request.UserId, beforeCreatedAt,
                beforeId, 100, cancellationToken).ConfigureAwait(false);
            digests.AddRange(page.Select(d => d.ToDto()));
            if (page.Count < 100) break;
            var cursor = page[^1];
            if (cursor.CreatedAt == beforeCreatedAt && cursor.Id == beforeId)
                throw new InvalidOperationException("Export page did not advance.");
            beforeCreatedAt = cursor.CreatedAt;
            beforeId = cursor.Id;
        }

        var subscriberDto = user.ToDto();
        var connectionDtos = user.Connections.Select(c => c.ToExportDto()).ToArray();
        var signalDtos = signals.Select(s => s.ToDto()).ToArray();

        return new UserDataExportDto(
            ExportVersion: "2.1",
            ExportedAtUtc: DateTime.UtcNow,
            Subscriber: subscriberDto,
            Connections: connectionDtos,
            TasteSignals: signalDtos,
            Digests: digests,
            LocalEmail: _archive is not null ? await _archive.ReadAsync(request.UserId, digests, cancellationToken)
                : new(digests.Any(d => d.Status == DigestStatus.LocalCaptured) ? "incomplete" : "disabled", [],
                    digests.Any(d => d.Status == DigestStatus.LocalCaptured) ? ["archive_unavailable"] : []));
    }
}
