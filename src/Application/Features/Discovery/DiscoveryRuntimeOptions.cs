namespace LinerNotes.Application.Features.Discovery;
public sealed class DiscoveryRuntimeOptions
{
    public bool RunJobs { get; set; }
    // Storage coordination must not turn off when a host pauses its background consumers.
    public bool EnforceStorageAdmission { get; set; }
    public bool ManagedWritesEnabled => RunJobs || EnforceStorageAdmission;
    public int QueueCapacity { get; set; } = 256;
    public int CooldownSeconds { get; set; } = 30;
    public int LookupCooldownSeconds { get; set; } = 1;
    public int LeaseSeconds { get; set; } = 60;
    public int MaximumAttempts { get; set; } = 3;
    public int MaximumWorkSeconds { get; set; } = 180;
    public int MaximumJobAgeSeconds { get; set; } = 600;
    public int AcquisitionConcurrency { get; set; } = 2;
    public int FeedbackWindow { get; set; } = 20;
    public long PublicCacheBytes { get; set; } = 256 * 1024 * 1024;
    public int PublicCacheEntries { get; set; } = 5000;
    public void Validate()
    {
        if (QueueCapacity is < 1 or > 10_000 || CooldownSeconds is < 1 or > 600 || LookupCooldownSeconds is < 1 or > 60 || LeaseSeconds is < 15 or > 120 ||
            MaximumAttempts is < 1 or > 10 || MaximumWorkSeconds is < 30 or > 600 || MaximumJobAgeSeconds is < 30 or > 3600 || AcquisitionConcurrency is < 1 or > 4 || FeedbackWindow is < 1 or > 40 ||
            PublicCacheBytes is < 1_048_576 or > 536_870_912 || PublicCacheEntries is < 10 or > 50_000)
            throw new ArgumentException("discovery_runtime_options_invalid");
    }
}
