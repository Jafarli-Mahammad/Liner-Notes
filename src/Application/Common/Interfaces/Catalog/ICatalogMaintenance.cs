namespace LinerNotes.Application.Common.Interfaces.Catalog;

public interface ICatalogMaintenance
{
    // Caller holds write admission. Delete at most limit records with their terms and ten empty publications.
    Task<int> DeleteExpiredAsync(DateTimeOffset now, int limit, CancellationToken ct);
}
