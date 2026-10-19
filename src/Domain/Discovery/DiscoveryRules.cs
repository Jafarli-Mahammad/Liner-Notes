namespace LinerNotes.Domain.Discovery;

internal static class DiscoveryRules
{
    public static Guid Identity(Guid value) => value != Guid.Empty ? value
        : throw new ArgumentException("A nonempty identity is required.");

    public static DateTimeOffset Utc(DateTimeOffset value) => value.Offset == TimeSpan.Zero ? value
        : throw new ArgumentException("UTC is required.");

    public static string RequestKey(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 80 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':') ? value
        : throw new ArgumentException("Invalid discovery request identity.");

    public static long Revision(long value) => value >= 0 ? value
        : throw new ArgumentException("Revision must not be negative.");
}
