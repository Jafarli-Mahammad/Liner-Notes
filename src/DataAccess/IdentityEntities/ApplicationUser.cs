using LinerNotes.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace LinerNotes.DataAccess.IdentityEntities;

/// <summary>
/// ASP.NET Core Identity user representing the authentication/credentials boundary.
/// Links 1:1 by Id to the Domain subscriber User aggregate.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>, IAuditableEntity
{
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? LastModifiedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsDeleted { get; set; }
}
