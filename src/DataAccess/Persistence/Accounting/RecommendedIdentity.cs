namespace LinerNotes.DataAccess.Persistence.Accounting;
/// <summary>Private compact suppression history. A recommendation does not assert a listen.</summary>
public sealed class RecommendedIdentity
{
    public Guid ProfileId { get; set; }
    public string Alias { get; set; } = "";
    public DateTimeOffset FirstRecommendedAtUtc { get; set; }
}
