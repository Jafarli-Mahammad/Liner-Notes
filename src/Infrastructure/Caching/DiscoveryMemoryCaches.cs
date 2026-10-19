using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace LinerNotes.Infrastructure.Caching;

public sealed class DiscoveryCacheOptions
{
    public long LastFmBytes { get; set; } = 16 * 1024 * 1024;
    public long MetadataBytes { get; set; } = 16 * 1024 * 1024;
    public long PreparedBytes { get; set; } = 32 * 1024 * 1024;
    public long TotalBytes { get; set; } = 64 * 1024 * 1024;
    public void Validate()
    {
        if (LastFmBytes < 65_536 || MetadataBytes < 65_536 || PreparedBytes < 65_536 || TotalBytes is < 196_608 or > 268_435_456 ||
            checked(LastFmBytes + MetadataBytes + PreparedBytes) > TotalBytes)
            throw new ArgumentException("discovery_cache_budget_invalid");
    }
}

/// <summary>All long-lived application evidence caches share one explicit aggregate budget.</summary>
public sealed class DiscoveryMemoryCaches : IDisposable
{
    public MemoryCache LastFm { get; }
    public MemoryCache Metadata { get; }
    public MemoryCache Prepared { get; }
    public long MaximumBytes { get; }
    public DiscoveryMemoryCaches(DiscoveryCacheOptions options)
    {
        options.Validate();
        MaximumBytes = checked(options.LastFmBytes + options.MetadataBytes + options.PreparedBytes);
        LastFm = Create(options.LastFmBytes); Metadata = Create(options.MetadataBytes); Prepared = Create(options.PreparedBytes);
    }
    private static MemoryCache Create(long bytes) => new(new MemoryCacheOptions
    { SizeLimit = bytes, CompactionPercentage = 0.20, ExpirationScanFrequency = TimeSpan.FromSeconds(30), TrackStatistics = true });

    // Conservative managed-retention estimate, not a measured process RSS claim. Includes UTF-16/object overhead.
    public static long EstimateBytes(long serializedBytes, string key)
    {
        if (serializedBytes < 0) throw new ArgumentOutOfRangeException(nameof(serializedBytes));
        return checked(serializedBytes * 4 + Encoding.UTF8.GetByteCount(key) * 2L + 1024);
    }
    public void Dispose() { LastFm.Dispose(); Metadata.Dispose(); Prepared.Dispose(); }
}
