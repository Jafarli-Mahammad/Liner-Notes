namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

/// <summary>
/// Operational mode for the Last.fm client subsystem.
/// </summary>
public enum LastFmClientMode
{
    /// <summary>
    /// Legacy configuration spelling for HTTP ingestion. Missing credentials or
    /// upstream failure return explicit gaps; no synthetic fallback.
    /// </summary>
    Hybrid = 0,

    /// <summary>
    /// HTTP ingestion with explicit failure gaps if offline or missing the API key.
    /// </summary>
    LiveOnly = 1,

    /// <summary>
    /// Exclusively uses explicitly synthetic fixtures (zero outbound network requests).
    /// </summary>
    FixtureOnly = 2
}

/// <summary>
/// Configuration options for the Last.fm recommendation and candidate hydration client.
/// </summary>
public sealed class LastFmOptions
{
    public const string SectionName = "LastFm";

    /// <summary>
    /// Last.fm Web Services API key.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Last.fm Shared Secret (optional for read-only unauthenticated calls).
    /// </summary>
    public string? SharedSecret { get; set; }

    /// <summary>
    /// Application name registered with Last.fm.
    /// </summary>
    public string ApplicationName { get; set; } = "Liner Notes";

    /// <summary>
    /// Account username registered with Last.fm.
    /// </summary>
    public string? RegisteredTo { get; set; }

    /// <summary>
    /// Last.fm API endpoint base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://ws.audioscrobbler.com/2.0/";

    /// <summary>
    /// Identifiable User-Agent header (required by Last.fm API terms of service).
    /// </summary>
    public string UserAgent { get; set; } = "LinerNotes/1.0 (+https://github.com/mahammadjafarli/LinerNotes)";

    /// <summary>
    /// Maximum allowed requests per second across the application to prevent rate-limit violations.
    /// Local pacing configuration only. Last.fm documents no universal numeric allowance.
    /// </summary>
    public int RequestsPerSecond { get; set; } = 4;

    /// <summary>
    /// Legacy configuration field retained for compatibility. Evidence envelopes
    /// are cached only within upstream HTTP freshness, without an adopted default lifetime.
    /// </summary>
    public int CacheDurationHours { get; set; } = 24;

    /// <summary>
    /// Operating mode (Hybrid, LiveOnly, FixtureOnly).
    /// </summary>
    public LastFmClientMode Mode { get; set; } = LastFmClientMode.Hybrid;
}
