using LinerNotes.Domain.Digest;

namespace LinerNotes.Application.Common.Interfaces.Repositories;

public interface IUserRepository : IAsyncRepository<User>
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetForExportAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetUsersDueForDigestAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);
    void Update(User user);
    Task<bool> DeleteOwnedDataAsync(Guid userId, CancellationToken cancellationToken = default);
}
