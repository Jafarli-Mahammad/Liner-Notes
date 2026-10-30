namespace LinerNotes.DataAccess.Persistence.Accounting;

public sealed class ManagedStorageCommitRecord
{
    public int Id { get; private set; }
    public bool Initialized { get; private set; }
    public Guid CommitId { get; private set; }
    public string BeforeInventoryJson { get; private set; } = "{}";
    public string ArtifactCategoriesJson { get; private set; } = "{}";
    public string OwnedChangesJson { get; private set; } = "[]";
    public string RootFingerprint { get; private set; } = "";
    public DateTimeOffset MeasuredAtUtc { get; private set; }
    public string ExpectedRevision { get; private set; } = "00000000000000000000";
    public string ExpectedLogicalBytes { get; private set; } = "00000000000000000000";
    public string ExpectedCoreRevision { get; private set; } = "00000000000000000000";
    public string ExpectedCoreLogicalBytes { get; private set; } = "00000000000000000000";
    public string ExpectedRowCount { get; private set; } = "00000000000000000000";
    public string SchemaFingerprint { get; private set; } = "";
}
