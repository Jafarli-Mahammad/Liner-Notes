namespace LinerNotes.Domain.Common;

/// <summary>
/// Abstract base entity capturing comprehensive UTC audit trails and soft-deletion state.
/// </summary>
public abstract class AuditableEntity : BaseEntity, IAuditableEntity
{
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? LastModifiedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsDeleted { get; set; }
}
