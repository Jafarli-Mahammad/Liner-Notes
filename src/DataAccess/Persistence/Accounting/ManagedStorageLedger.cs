namespace LinerNotes.DataAccess.Persistence.Accounting;
/// <summary>Global operational accounting. PostgreSQL triggers maintain it; it contains no user payload.</summary>
public sealed class ManagedStorageLedger
{
    public int Id { get; private set; }
    public long CoreRevision { get; private set; }
    public long CoreLogicalBytes { get; private set; }
    public long Revision { get; private set; }
    public long LogicalBytes { get; private set; }
    public long RowCount { get; private set; }
}
