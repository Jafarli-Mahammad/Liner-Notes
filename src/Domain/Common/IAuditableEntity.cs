namespace LinerNotes.Domain.Common;

/// <summary>
/// Audit trail contract capturing record creation and update timestamps in UTC.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime? LastModifiedAt { get; set; }
}
