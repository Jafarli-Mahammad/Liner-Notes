using LinerNotes.Domain.Common;
using LinerNotes.Domain.Enums;

namespace LinerNotes.Domain.Digest;

/// <summary>
/// Record linking a user account with an external music provider (Last.fm, ListenBrainz).
/// </summary>
public sealed class UserMusicConnection : BaseEntity, IAuditableEntity
{
    public Guid UserId { get; private set; }
    public MusicServiceType ServiceType { get; private set; }
    public string ExternalUsername { get; private set; }
    public string? EncryptedToken { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedAt { get; set; }

    // Parameterless constructor for EF Core
    private UserMusicConnection()
    {
        ExternalUsername = string.Empty;
    }

    public UserMusicConnection(
        Guid userId,
        MusicServiceType serviceType,
        string externalUsername,
        string? encryptedToken = null,
        Guid? id = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (string.IsNullOrWhiteSpace(externalUsername))
            throw new ArgumentException("External username cannot be empty.", nameof(externalUsername));

        if (id.HasValue) Id = id.Value;
        UserId = userId;
        ServiceType = serviceType;
        ExternalUsername = externalUsername.Trim();
        EncryptedToken = string.IsNullOrWhiteSpace(encryptedToken) ? null : encryptedToken.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    public static UserMusicConnection Create(
        Guid userId,
        MusicServiceType serviceType,
        string externalUsername,
        string? encryptedToken = null) =>
        new(userId, serviceType, externalUsername, encryptedToken);

    public void MarkSynced(DateTime syncedAtUtc)
    {
        LastSyncedAt = syncedAtUtc;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void UpdateToken(string? encryptedToken)
    {
        EncryptedToken = string.IsNullOrWhiteSpace(encryptedToken) ? null : encryptedToken.Trim();
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        LastModifiedAt = DateTime.UtcNow;
    }

    public override string ToString() => $"{ServiceType}: {ExternalUsername} (Active: {IsActive})";
}
