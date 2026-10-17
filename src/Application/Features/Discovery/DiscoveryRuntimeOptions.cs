namespace LinerNotes.Application.Features.Discovery;
public sealed class DiscoveryRuntimeOptions
{
    public bool RunJobs { get; set; }
    public int QueueCapacity { get; set; } = 256;
    public int CooldownSeconds { get; set; } = 30;
    public int LeaseSeconds { get; set; } = 60;
    public int MaximumAttempts { get; set; } = 3;
    public int MaximumWorkSeconds { get; set; } = 180;
    public int FeedbackWindow { get; set; } = 20;
    public long PublicCacheBytes { get; set; } = 256 * 1024 * 1024;
    public int PublicCacheEntries { get; set; } = 5000;
    public void Validate()
    {
        if (QueueCapacity is < 1 or > 10_000 || CooldownSeconds is < 1 or > 600 || LeaseSeconds is < 15 or > 120 ||
            MaximumAttempts is < 1 or > 10 || MaximumWorkSeconds is < 30 or > 600 || FeedbackWindow is < 1 or > 40 ||
            PublicCacheBytes is < 1_048_576 or > 536_870_912 || PublicCacheEntries is < 10 or > 50_000)
            throw new ArgumentException("discovery_runtime_options_invalid");
    }
}
