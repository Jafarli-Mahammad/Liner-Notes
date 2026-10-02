namespace LinerNotes.Presentation.Options;

/// <summary>
/// Configuration options for JSON Web Token generation and validation.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "LinerNotes";
    public string Audience { get; set; } = "LinerNotesAudience";
    public int ExpiryMinutes { get; set; } = 1440; // 24 hours default
}
