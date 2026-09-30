namespace LinerNotes.Domain.Enums;

/// <summary>
/// Execution lifecycle status of a weekly recommendation digest.
/// </summary>
public enum DigestStatus
{
    Pending = 0,
    InProgress = 1,
    Sent = 2,
    Failed = 3
}
