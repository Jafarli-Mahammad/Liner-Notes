using System.Collections;
using System.Collections.Immutable;
using LinerNotes.Application.Common.Models.Recommendation;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;

public sealed class LastFmResponse<T> : IReadOnlyList<T>
{
    public ImmutableArray<T> Items { get; }
    public ImmutableArray<CoverageGap> Gaps { get; }
    public ResponseReference Reference { get; }
    public int Count => Items.Length;
    public T this[int index] => Items[index];
    public LastFmResponse(IEnumerable<T> items, ResponseReference reference, IEnumerable<CoverageGap>? gaps = null)
    {
        Items = items.ToImmutableArray(); Reference = reference; Gaps = gaps?.ToImmutableArray() ?? [];
    }
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
