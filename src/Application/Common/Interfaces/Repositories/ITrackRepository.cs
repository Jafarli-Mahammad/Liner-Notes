using LinerNotes.Domain.Catalog;

namespace LinerNotes.Application.Common.Interfaces.Repositories;

public interface ITrackRepository : IAsyncRepository<Track>
{
    Task<Track?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Track?> GetByMbidAsync(string mbid, CancellationToken cancellationToken = default);
    Task<Track?> FindByArtistAndTitleAsync(string artistName, string title, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Track> tracks, CancellationToken cancellationToken = default);
}
