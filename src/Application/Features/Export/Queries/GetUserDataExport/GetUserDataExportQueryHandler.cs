using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
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
    private readonly IAccountProfileReader _accountProfileReader;
    private readonly IDiscoveryProfileReader _discoveryProfileReader;

    /// <summary>Configures export readers and an optional local email archive.</summary>
    /// <remarks>When <paramref name="archive"/> is absent, exports report the archive as incomplete for captured digests, or disabled otherwise.</remarks>
    public GetUserDataExportQueryHandler(
        IUserRepository userRepository,
        ITasteSignalRepository tasteSignalRepository,
        IWeeklyDigestRepository weeklyDigestRepository, IAccountProfileReader accountProfileReader,
        IDiscoveryProfileReader discoveryProfileReader,
        ILocalEmailArchive? archive = null)
    {
        _archive = archive;
        _userRepository = userRepository;
        _tasteSignalRepository = tasteSignalRepository;
        _weeklyDigestRepository = weeklyDigestRepository;
        _accountProfileReader = accountProfileReader;
        _discoveryProfileReader = discoveryProfileReader;
    }

    /// <summary>Returns version 2.4 account, discovery, taste and digest history with explicit security exclusions.</summary>
    /// <remarks>Repository and archive exceptions, including cancellation, propagate; an archive result marked incomplete is retained in the export.</remarks>
    /// <exception cref="NotFoundException">The subscriber or active Identity profile cannot be found.</exception>
    /// <exception cref="InvalidOperationException">A full digest page repeats the preceding cursor.</exception>
    public async Task<UserDataExportDto> Handle(
        GetUserDataExportQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetForExportAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException(nameof(User), request.UserId);
        }

        var account = await _accountProfileReader.GetAsync(request.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Account", request.UserId);

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
            ExportVersion: "2.4",
            ExportedAtUtc: DateTime.UtcNow,
            Subscriber: subscriberDto,
            Connections: connectionDtos,
            TasteSignals: signalDtos,
            Digests: digests,
            LocalEmail: _archive is not null ? await _archive.ReadAsync(request.UserId, digests, cancellationToken)
                : new(digests.Any(d => d.Status == DigestStatus.LocalCaptured) ? "incomplete" : "disabled", [],
                    digests.Any(d => d.Status == DigestStatus.LocalCaptured) ? ["archive_unavailable"] : []),
            Account: account,
            Discovery: await _discoveryProfileReader.GetForAccountAsync(request.UserId, cancellationToken).ConfigureAwait(false));
    }
}
