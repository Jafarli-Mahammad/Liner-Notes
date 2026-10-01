using LinerNotes.Domain.Common;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;

namespace LinerNotes.Domain.Digest;

/// <summary>
/// Aggregate root representing an individual Liner Notes subscriber.
/// </summary>
public sealed class User : AuditableEntity
{
    private readonly List<UserMusicConnection> _connections = new();
    private readonly List<TasteSignal> _tasteSignals = new();

    public string Email { get; private set; }
    public string TimeZone { get; private set; }
    public DigestDeliveryDay DeliveryDay { get; private set; }
    public int DeliveryHourUtc { get; private set; }
    public DateTime? NextDigestAt { get; private set; }

    public IReadOnlyCollection<UserMusicConnection> Connections => _connections.AsReadOnly();
    public IReadOnlyCollection<TasteSignal> TasteSignals => _tasteSignals.AsReadOnly();

    // Parameterless constructor for EF Core
    private User()
    {
        Email = string.Empty;
        TimeZone = "UTC";
    }

    public User(
        string email,
        string timeZone = "UTC",
        DigestDeliveryDay deliveryDay = DigestDeliveryDay.Sunday,
        int deliveryHourUtc = 8,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));
        if (deliveryHourUtc < 0 || deliveryHourUtc > 23)
            throw new ArgumentOutOfRangeException(nameof(deliveryHourUtc), "Delivery hour must be between 0 and 23.");

        if (id.HasValue) Id = id.Value;
        Email = email.Trim().ToLowerInvariant();
        TimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone.Trim();
        DeliveryDay = deliveryDay;
        DeliveryHourUtc = deliveryHourUtc;
        CreatedAt = DateTime.UtcNow;
    }

    public static User Create(string email, string timeZone = "UTC") => new(email, timeZone);

    public void UpdateSchedule(DigestDeliveryDay deliveryDay, int deliveryHourUtc, string timeZone)
    {
        if (deliveryHourUtc < 0 || deliveryHourUtc > 23)
            throw new ArgumentOutOfRangeException(nameof(deliveryHourUtc), "Delivery hour must be between 0 and 23.");

        DeliveryDay = deliveryDay;
        DeliveryHourUtc = deliveryHourUtc;
        TimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone.Trim();
        LastModifiedAt = DateTime.UtcNow;
    }

    public void SetNextDigestAt(DateTime nextDigestAt)
    {
        NextDigestAt = nextDigestAt;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void AddConnection(UserMusicConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (_connections.Any(c => c.ServiceType == connection.ServiceType))
            throw new InvalidOperationException($"User already has a connected {connection.ServiceType} account.");

        _connections.Add(connection);
        LastModifiedAt = DateTime.UtcNow;
    }

    public void AddTasteSignal(TasteSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        _tasteSignals.Add(signal);
        LastModifiedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        LastModifiedAt = DateTime.UtcNow;
    }

    public override string ToString() => Email;
}
