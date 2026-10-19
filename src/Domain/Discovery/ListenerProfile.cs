using LinerNotes.Domain.Common;

namespace LinerNotes.Domain.Discovery;

/// <summary>A taste owner independent of registration and email delivery.</summary>
public sealed class ListenerProfile : BaseEntity
{
    public Guid? UserId { get; private set; }
    public long Revision { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastActiveAtUtc { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public string? OnboardingJson { get; private set; }
    public void SetOnboarding(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 4096) throw new ArgumentException("onboarding_too_large");
        OnboardingJson = json;
    }
    public bool IsGuest => UserId is null;

    private ListenerProfile() { }

    public static ListenerProfile Guest(DateTimeOffset now, TimeSpan inactivity, Guid? id = null)
    {
        if (inactivity <= TimeSpan.Zero) throw new ArgumentException("Positive inactivity lifetime required.");
        now = DiscoveryRules.Utc(now);
        return new()
        {
            Id = DiscoveryRules.Identity(id ?? Guid.NewGuid()),
            CreatedAtUtc = now,
            LastActiveAtUtc = now,
            ExpiresAtUtc = now.Add(inactivity)
        };
    }

    public static ListenerProfile Registered(Guid userId, DateTimeOffset now, Guid? id = null)
    {
        userId = DiscoveryRules.Identity(userId);
        now = DiscoveryRules.Utc(now);
        return new()
        {
            Id = DiscoveryRules.Identity(id ?? userId),
            UserId = userId,
            CreatedAtUtc = now,
            LastActiveAtUtc = now
        };
    }

    public bool IsExpired(DateTimeOffset now) => ExpiresAtUtc is { } expiry && expiry <= DiscoveryRules.Utc(now);

    public bool Touch(DateTimeOffset now, TimeSpan inactivity, TimeSpan minimumInterval)
    {
        now = DiscoveryRules.Utc(now);
        if (inactivity <= TimeSpan.Zero || minimumInterval < TimeSpan.Zero)
            throw new ArgumentException("Invalid activity intervals.");
        if (IsExpired(now)) throw new InvalidOperationException("guest_expired");
        if (now <= LastActiveAtUtc || now - LastActiveAtUtc < minimumInterval) return false;
        LastActiveAtUtc = now;
        if (IsGuest) ExpiresAtUtc = now.Add(inactivity);
        return true;
    }

    public void Attach(Guid userId, DateTimeOffset now)
    {
        userId = DiscoveryRules.Identity(userId);
        now = DiscoveryRules.Utc(now);
        if (IsExpired(now)) throw new InvalidOperationException("guest_expired");
        if (UserId is { } existing && existing != userId) throw new InvalidOperationException("profile_already_attached");
        UserId = userId;
        ExpiresAtUtc = null;
        if (now > LastActiveAtUtc) LastActiveAtUtc = now;
    }

    public void ChangeInputs(long expectedRevision)
    {
        if (Revision != expectedRevision) throw new InvalidOperationException("profile_revision_conflict");
        Revision = checked(Revision + 1);
    }
}
