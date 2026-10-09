using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;

namespace LinerNotes.Application.Common.Interfaces.Recommendation;

public sealed record GenerationInputs(bool UserExists, IReadOnlyList<TasteSignal> Signals,
    IReadOnlyList<WeeklyRecommendation> Feedback, string Revision);
public sealed record GenerationStorageState(long DatabaseBytes, string Revision);

public interface IDigestGenerationStore
{
    Task<IAsyncDisposable> AcquireBatchAsync(CancellationToken cancellationToken = default);
    Task<WeeklyDigest?> GetExistingAsync(Guid userId, IsoWeek week, CancellationToken cancellationToken = default);
    Task<GenerationInputs> ReadInputsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<GenerationStorageState> ReadStorageAsync(CancellationToken cancellationToken = default);
    Task<WeeklyDigest> PersistAsync(Guid userId, IsoWeek week, string inputRevision,
        IReadOnlyList<BaselineAPick> picks, IGenerationStorageReservation reservation,
        CancellationToken cancellationToken = default);
}

public interface ISeedTagSource
{
    Task<SeedTagSnapshot> GetAsync(string artistName, CancellationToken cancellationToken = default);
}

public sealed record SeedTagSnapshot(string ArtistName, LinerNotes.Domain.Catalog.WeightedTagVector Vector,
    IReadOnlyList<TagObservation> Tags, ResponseReference Response, string? MissingReason, IReadOnlyList<CoverageGap> Gaps);

public interface IGenerationStorage
{
    Task<IGenerationStorageLease> AcquireAsync(GenerationStorageState database, CancellationToken cancellationToken = default);
}

public interface IGenerationStorageLease : IAsyncDisposable
{
    Task<IGenerationStorageReservation> ReserveAsync(long serializedBytes, int pickCount,
        GenerationStorageState database, CancellationToken cancellationToken = default);
}

public interface IGenerationStorageReservation
{
    Task ValidateAsync(GenerationStorageState database, CancellationToken cancellationToken = default);
    Task VerifyStoredAsync(long actualColumnBytes, GenerationStorageState database,
        CancellationToken cancellationToken = default);
}

public sealed class GenerationStoppedException(string reason) : Exception(reason);
