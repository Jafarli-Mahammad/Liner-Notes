namespace LinerNotes.Domain.Common;

/// <summary>
/// Audit trail contract capturing record creation and update timestamps in UTC.
/// </summary>
public interface IAuditableEntity
{
    Guid? CreatedBy { get; set; }
    DateTime CreatedAt { get; set; }
    Guid? LastModifiedBy { get; set; }
    DateTime? LastModifiedAt { get; set; }
    Guid? DeletedBy { get; set; }
    DateTime? DeletedAt { get; set; }
    bool IsDeleted { get; set; }
}
