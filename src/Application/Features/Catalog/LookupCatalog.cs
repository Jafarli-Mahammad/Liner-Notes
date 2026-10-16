using LinerNotes.Domain.Catalog;
using MediatR;

namespace LinerNotes.Application.Features.Catalog;

public sealed record CatalogLookupStatus(Guid Id, string State, string ProgressCode, string? ErrorCode,
    IReadOnlyList<LinerNotes.Application.Common.Interfaces.Catalog.CatalogItemDto>? Items = null);
public interface ICatalogLookupJobs
{
    bool AcceptingRequests { get; }
    Task<CatalogLookupStatus> SubmitAsync(Guid profileId, string query, CatalogKind kind, string requestKey, CancellationToken ct);
    Task<CatalogLookupStatus?> FindAsync(Guid profileId, Guid jobId, CancellationToken ct);
}
public sealed record SubmitCatalogLookup(Guid ProfileId, string Query, CatalogKind Kind, string RequestKey) : IRequest<CatalogLookupStatus>;
public sealed class SubmitCatalogLookupHandler(ICatalogLookupJobs jobs) : IRequestHandler<SubmitCatalogLookup, CatalogLookupStatus>
{
    public Task<CatalogLookupStatus> Handle(SubmitCatalogLookup request, CancellationToken ct)
    {
        if (request.ProfileId == Guid.Empty || !Enum.IsDefined(request.Kind) || string.IsNullOrWhiteSpace(request.Query) ||
            request.Query.Length > 128 || request.Query.Any(char.IsControl) || string.IsNullOrWhiteSpace(request.RequestKey) ||
            request.RequestKey.Length > 80 || request.RequestKey.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or ':')))
            throw new ArgumentException("catalog_lookup_request_invalid");
        if (!jobs.AcceptingRequests) throw new LinerNotes.Application.Common.Interfaces.Recommendation.GenerationStoppedException("catalog_lookup_worker_not_ready");
        return jobs.SubmitAsync(request.ProfileId, request.Query.Trim(), request.Kind, request.RequestKey, ct);
    }
}
public sealed record GetCatalogLookup(Guid ProfileId, Guid JobId) : IRequest<CatalogLookupStatus?>;
public sealed class GetCatalogLookupHandler(ICatalogLookupJobs jobs) : IRequestHandler<GetCatalogLookup, CatalogLookupStatus?>
{
    public Task<CatalogLookupStatus?> Handle(GetCatalogLookup request, CancellationToken ct) => jobs.FindAsync(request.ProfileId, request.JobId, ct);
}
