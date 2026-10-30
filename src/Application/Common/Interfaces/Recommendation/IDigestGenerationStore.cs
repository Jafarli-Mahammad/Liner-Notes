using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;

namespace LinerNotes.Application.Common.Interfaces.Recommendation;

public sealed record GenerationInputs(bool UserExists, IReadOnlyList<TasteSignal> Signals,
    IReadOnlyList<WeeklyRecommendation> Feedback, string Revision, Guid? ProfileId = null);
public sealed record GenerationStorageState(long DatabaseBytes, string Revision, long ReservedArtifactBytes = 0);

public interface IDigestGenerationStore
{
    Task<IAsyncDisposable> AcquireBatchAsync(CancellationToken cancellationToken = default);
    Task<WeeklyDigest?> GetExistingAsync(Guid userId, IsoWeek week, CancellationToken cancellationToken = default);
    Task<GenerationInputs> ReadInputsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<GenerationStorageState> ReadStorageAsync(CancellationToken cancellationToken = default);
    // Caller owns coordinated write admission and publishes its committed inventory.
    Task<WeeklyDigest> PersistWithinAdmissionAsync(Guid userId, IsoWeek week, string inputRevision,
        IReadOnlyList<BaselineAPick> picks, CancellationToken cancellationToken = default);
    Task<WeeklyDigest> PersistAsync(Guid userId, IsoWeek week, string inputRevision,
        IReadOnlyList<BaselineAPick> picks, IGenerationStorageReservation reservation,
        CancellationToken cancellationToken = default);
}

public interface ISeedTagSource
{
    Task<SeedTagSnapshot> GetAsync(string artistName, CancellationToken cancellationToken = default);
}

public sealed record SeedTagSnapshot(string ArtistName, LinerNotes.Domain.Catalog.WeightedTagVector Vector,
    IReadOnlyList<TagObservation> Tags, ResponseReference Response, string? MissingReason, IReadOnlyList<CoverageGap> Gaps)
{
    public Guid? CanonicalArtistId { get; init; }
    public IReadOnlyList<ResponseReference> SupportingResponses { get; init; } = [];
    public DateTimeOffset? SharedUntilUtc { get; init; }
}

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
