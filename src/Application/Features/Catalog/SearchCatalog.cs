using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Domain.Catalog;
using MediatR;

namespace LinerNotes.Application.Features.Catalog;

public sealed record SearchCatalog(string Query, CatalogKind Kind = CatalogKind.Artist, int Limit = 10, int Offset = 0)
    : IRequest<CatalogSearchResult>;
public sealed class SearchCatalogHandler(ICatalogReader catalog, TimeProvider clock) : IRequestHandler<SearchCatalog, CatalogSearchResult>
{
    public Task<CatalogSearchResult> Handle(SearchCatalog request, CancellationToken ct) =>
        catalog.SearchAsync(request.Query ?? "", request.Kind, request.Limit, request.Offset, clock.GetUtcNow(), ct);
}
