namespace LinerNotes.Domain.Email;

public sealed class EmailSuppression
{
    public Guid UserId { get; private set; }
    public string RecipientHash { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    private EmailSuppression() { }
    public EmailSuppression(Guid userId, string recipientHash, string reason, DateTimeOffset now)
    { UserId = userId; RecipientHash = recipientHash; Reason = reason; CreatedAt = now; }
}
