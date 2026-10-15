using System.Globalization;
using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;

public interface IMetaBrainzRequestGate
{
    Task WaitAsync(string provider, CancellationToken cancellationToken);
    Task DeferAsync(string provider, TimeSpan delay, CancellationToken cancellationToken);
}

/// <summary>Shared filesystem pacing for local processes/containers on the same upstream IP.</summary>
public sealed class MetaBrainzRequestGate(ListenBrainzOptions options, TimeProvider clock) : IMetaBrainzRequestGate
{
    public Task WaitAsync(string provider, CancellationToken cancellationToken) => UpdateAsync(provider, null, cancellationToken);
    public Task DeferAsync(string provider, TimeSpan delay, CancellationToken cancellationToken) => UpdateAsync(provider, delay, cancellationToken);

    private async Task UpdateAsync(string provider, TimeSpan? defer, CancellationToken ct)
    {
        options.Validate();
        string fileName = provider switch { "ListenBrainz" => "listenbrainz", "MusicBrainz" => "musicbrainz", _ => throw new ArgumentException("provider_invalid") };
        string directory = options.RateLimitDirectory;
        Directory.CreateDirectory(directory);
        for (string? path = directory; path != null; path = Path.GetDirectoryName(path))
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new GenerationStoppedException("upstream_pacing_symlink");
        string file = Path.Combine(directory, fileName + ".pace");
        for (int attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            TimeSpan wait = TimeSpan.FromMilliseconds(50);
            try
            {
                if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new GenerationStoppedException("upstream_pacing_symlink");
                using var stream = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                if (stream.Length > 100) throw new GenerationStoppedException("upstream_pacing_invalid");
                using var reader = new StreamReader(stream, leaveOpen: true);
                string raw = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                var now = clock.GetUtcNow();
                long ticks = 0;
                if (raw.Length > 0 && (!long.TryParse(raw, CultureInfo.InvariantCulture, out ticks) || ticks < 0 || ticks > DateTimeOffset.MaxValue.Ticks))
                    throw new GenerationStoppedException("upstream_pacing_invalid");
                var next = new DateTimeOffset(ticks, TimeSpan.Zero);
                if (defer.HasValue || now >= next)
                {
                    var target = now + (defer ?? TimeSpan.FromSeconds(1.05));
                    if (next > target) target = next;
                    byte[] bytes = System.Text.Encoding.ASCII.GetBytes(target.Ticks.ToString(CultureInfo.InvariantCulture));
                    stream.Position = 0; stream.SetLength(0);
                    await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
                    stream.Flush(true);
                    return;
                }
                wait = next - now;
                if (wait > TimeSpan.FromSeconds(30)) throw new GenerationStoppedException("upstream_backoff_active");
            }
            catch (IOException) when (attempt < 600) { }
            await Task.Delay(wait, clock, ct).ConfigureAwait(false);
        }
    }
}
