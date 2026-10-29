using System.Security.Cryptography;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.DataAccess.Core;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.Persistence.Repositories;
using LinerNotes.Infrastructure.Artwork;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using LinerNotes.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal static partial class Audit
{
    static async Task ArtworkRestart(bool persist)
    {
        var connection = Environment.GetEnvironmentVariable("LINER_PROBE_POSTGRES") ?? throw new ArgumentException("Explicit disposable database required");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "liner_design_artwork")
            throw new ArgumentException("Only the dedicated local artwork experiment database is supported");
        string directory = Environment.GetEnvironmentVariable("LINER_PROBE_ROOT") ?? throw new ArgumentException("Explicit experiment root required");
        if (!System.IO.Path.IsPathFullyQualified(directory) || !System.IO.Path.GetFileName(directory).StartsWith("liner-artwork-probe-", StringComparison.Ordinal))
            throw new ArgumentException("A uniquely named experiment directory is required");
        await using var db = new DataContext(new DbContextOptionsBuilder<DataContext>().UseNpgsql(connection).Options);
        var clock = TimeProvider.System; var unit = new UnitOfWork(db); var inventory = new DigestGenerationStore(db, unit);
        var storage = new LocalGenerationStorage(StorageOptions(directory), clock, new() { FilesystemPaths = [directory] });
        var admission = new DiscoveryWriteCoordinator(db, unit, inventory, storage);
        var stored = new StoredArtwork(new PublicEvidenceStore(db), admission, clock);
        var id = Guid.Parse("7eed5306-2e20-4557-96b0-26423d543918"); // Generated public fixture identity only.
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS1sAAAAASUVORK5CYII=");
        if (persist)
        {
            await storage.ReconcileAsync(await inventory.ReadStorageAsync()); // Explicit test-fixture setup, not product recovery.
            await stored.SaveAsync(id, new(bytes, "image/png", clock.GetUtcNow().AddMinutes(30)), default);
            Emit(new { scenario = "artwork-process-persist", persisted = true });
            return;
        }
        using var cache = new RecordingArtworkCache();
        var network = new ForbiddenArtworkNetwork();
        var artwork = new RecordingArtwork(cache, network, network, network,
            new() { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true }, clock, stored);
        await artwork.WarmAsync([id], default, allowAcquisition: false);
        var cover = artwork.GetCached(id) ?? throw new Exception("restart_cover_missing");
        if (!cover.Bytes.SequenceEqual(bytes)) throw new Exception("restart_cover_changed");
        Emit(new { scenario = "artwork-process-restore", restored = true, providerRequests = 0,
            bytes = cover.Bytes.Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(cover.Bytes)) });
    }

    sealed class ForbiddenArtworkNetwork : IListenBrainzApiClient, IHttpClientFactory, IMetaBrainzRequestGate
    {
        public Task<ListenBrainzResponse> SendAsync(ListenBrainzRequest request, CancellationToken ct = default) => throw new Exception("unexpected_metadata_request");
        public HttpClient CreateClient(string name) => throw new Exception("unexpected_image_request");
        public Task WaitAsync(string provider, CancellationToken ct) => throw new Exception("unexpected_provider_gate");
        public Task DeferAsync(string provider, TimeSpan delay, CancellationToken ct) => throw new Exception("unexpected_provider_deferral");
    }
}
