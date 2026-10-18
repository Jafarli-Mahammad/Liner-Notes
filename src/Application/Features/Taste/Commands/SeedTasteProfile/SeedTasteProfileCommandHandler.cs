using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Taste;
using MediatR;

namespace LinerNotes.Application.Features.Taste.Commands.SeedTasteProfile;

/// <summary>
/// Handler persisting atomic taste signals for initial user onboarding.
/// </summary>
public sealed class SeedTasteProfileCommandHandler : IRequestHandler<SeedTasteProfileCommand, int>
{
    private readonly IUserRepository _userRepository;
    private readonly ITasteSignalRepository _tasteSignalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IListenerProfileRepository _profiles;

    public SeedTasteProfileCommandHandler(
        IUserRepository userRepository,
        ITasteSignalRepository tasteSignalRepository,
        IUnitOfWork unitOfWork, IListenerProfileRepository profiles)
    {
        _userRepository = userRepository;
        _tasteSignalRepository = tasteSignalRepository;
        _unitOfWork = unitOfWork;
        _profiles = profiles;
    }

    public async Task<int> Handle(
        SeedTasteProfileCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException(nameof(User), request.UserId);
        }

        var signals = new List<TasteSignal>();

        if (request.Tags != null)
        {
            foreach (var tag in request.Tags)
            {
                signals.Add(TasteSignal.CreateSeedTag(
                    request.UserId,
                    tag.Tag,
                    tag.Weight,
                    tag.Context ?? "Manual onboarding seed tag"));
            }
        }

        if (request.Artists != null)
        {
            foreach (var artist in request.Artists)
            {
                signals.Add(TasteSignal.CreateSeedArtist(
                    request.UserId,
                    artist.ArtistName,
                    artist.Weight,
                    artist.Context ?? "Manual onboarding seed artist"));
            }
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _profiles.ChangeInputsAsync(request.UserId, ct);
            await _tasteSignalRepository.AddRangeAsync(signals, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return signals.Count;
        }, cancellationToken);
    }
}
