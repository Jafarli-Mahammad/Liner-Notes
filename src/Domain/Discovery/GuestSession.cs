namespace LinerNotes.Domain.Discovery;

/// <summary>Only a high entropy credential's hash is persisted. The profile owns its lifetime.</summary>
public sealed class GuestSession
{
    public Guid ProfileId { get; private set; }
    public string CredentialHash { get; private set; } = "";
    public DateTimeOffset CreatedAtUtc { get; private set; }
    private GuestSession() { }
    public GuestSession(Guid profileId, string credentialHash, DateTimeOffset now)
    {
        ProfileId = DiscoveryRules.Identity(profileId);
        if (credentialHash.Length != 64 || credentialHash.Any(c => c is not (>= 'a' and <= 'f') and not (>= '0' and <= '9')))
            throw new ArgumentException("Invalid credential hash.");
        CredentialHash = credentialHash;
        CreatedAtUtc = DiscoveryRules.Utc(now);
    }
}
