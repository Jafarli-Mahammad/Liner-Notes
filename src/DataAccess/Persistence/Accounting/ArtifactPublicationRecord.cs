using LinerNotes.Application.Common.Interfaces.Discovery;

namespace LinerNotes.DataAccess.Persistence.Accounting;

public sealed class ArtifactPublicationRecord
{
    public Guid Id { get; set; }
    public string Category { get; set; } = "";
    public string Root { get; set; } = "";
    public string FinalName { get; set; } = "";
    public string StageName { get; set; } = "";
    public long ReservedBytes { get; set; }
    public long ContentBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public ArtifactPublicationPhase Phase { get; set; }
    public long Version { get; set; }
    public Guid? OwnerId { get; set; }
    public long LastWriteTimeUtcTicks { get; set; }
}
