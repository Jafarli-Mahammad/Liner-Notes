using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.DataAccess.Core;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

/// <summary>
/// Repository managing musical catalog tracks and deduplication lookups by MBID or normalized artist/title.
/// </summary>
public sealed class TrackRepository : AsyncRepository<Track>, ITrackRepository
{
    public TrackRepository(DataContext context) : base(context)
    {
    }

    public async Task<Track?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DataContext.Tracks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Track?> GetByMbidAsync(string mbid, CancellationToken cancellationToken = default)
    {
        var trimmed = mbid.Trim();
        return await DataContext.Tracks.FirstOrDefaultAsync(t => t.Mbid == trimmed, cancellationToken);
    }

    public async Task<Track?> FindByArtistAndTitleAsync(string artistName, string title, CancellationToken cancellationToken = default)
    {
        var normalizedArtist = artistName.Trim().ToLowerInvariant();
        var normalizedTitle = title.Trim().ToLowerInvariant();

        return await DataContext.Tracks.FirstOrDefaultAsync(
            t => t.NormalizedArtistName == normalizedArtist && t.NormalizedTitle == normalizedTitle,
            cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Track> tracks, CancellationToken cancellationToken = default)
    {
        await DataContext.Tracks.AddRangeAsync(tracks, cancellationToken);
    }
}
