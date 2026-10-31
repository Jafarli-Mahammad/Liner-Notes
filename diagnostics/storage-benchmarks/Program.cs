// Launch-only, disposable fixtures. No production attachment or provider calls.
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime;
using System.Security.Cryptography;
using System.Text.Json;
using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Application.Features.Export.Queries.GetUserDataExport;
using LinerNotes.Application.Services;
using LinerNotes.DataAccess;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Storage;
using LinerNotes.Presentation;
using LinerNotes.Presentation.Controllers;
using LinerNotes.Presentation.Filters;
using LinerNotes.Presentation.Models;
using LinerNotes.Presentation.Options;
using LinerNotes.Presentation.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

internal static class Program
{
    private const string Password = "Disposable-storage-benchmark-only-123!";
    private static readonly AsyncLocal<Sample?> Current = new();
    private static async Task Main(string[] args)
    {
        if (args is not ["--disposable-only"]) throw new ArgumentException("Explicit disposable-only mode required.");
        string admin = Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES") ?? throw new ArgumentException("Owned loopback database required.");
        var settings = new NpgsqlConnectionStringBuilder(admin);
        if (settings.Host is not ("127.0.0.1" or "localhost" or "::1")) throw new ArgumentException("Loopback required.");
        int samples = int.Parse(Environment.GetEnvironmentVariable("LINER_BENCH_SAMPLES") ?? "20");
        if (samples is < 4 or > 100) throw new ArgumentException("Use 4-100 samples.");
        string shape = Environment.GetEnvironmentVariable("LINER_BENCH_SHAPE") ?? "empty";
        (long bytes, int files) = shape switch
        {
            "empty" => (0, 0), "100m" => (100_000_000, 1), "900m" => (900_000_000, 1),
            "900m-1000" => (900_000_000, 1000), _ => throw new ArgumentException("Unknown shape.")
        };
        string parent = Environment.GetEnvironmentVariable("LINER_STORAGE_PROBE_ROOT") ?? Path.GetTempPath();
        string root = Path.Combine(parent, "liner-storage-bench-" + Guid.NewGuid().ToString("N"));
        string database = "liner_storage_bench_" + Guid.NewGuid().ToString("N");
        ServiceProvider? services = null;
        bool created = false;
        using var meter = new MeterListener();
        meter.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "LinerNotes.Discovery") listener.EnableMeasurementEvents(instrument);
        };
        meter.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            if (Current.Value is not { } sample || instrument.Name != "discovery.stage.duration") return;
            foreach (var tag in tags) if (tag.Key == "stage" && tag.Value is string stage) sample.Record(stage, value);
        });
        meter.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (Current.Value is not { } sample) return;
            if (instrument.Name == "storage.content.bytes") Interlocked.Add(ref sample.HashBytes, value);
            if (instrument.Name == "storage.namespace.scans") Interlocked.Add(ref sample.Scans, value);
        });
        meter.Start();
        try
        {
            Directory.CreateDirectory(root);
            await using (var db = new NpgsqlConnection(admin))
            {
                await db.OpenAsync();
                await new NpgsqlCommand($"CREATE DATABASE {database}", db).ExecuteNonQueryAsync();
                created = true;
            }
            settings.Database = database; settings.Pooling = true; settings.MaxPoolSize = 32;
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = settings.ConnectionString,
                ["Jwt:SecretKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                ["Jwt:Issuer"] = "fixture", ["Jwt:Audience"] = "fixture",
                ["LastFm:Mode"] = "FixtureOnly", ["Generation:Origin"] = "Synthetic"
            }).Build();
            var options = new GenerationStorageOptions
            {
                InventoryPath = Path.Combine(root, "inventory.json"), LeasePath = Path.Combine(root, "lease"),
                DatabaseOverheadBytesPerPick = 1_000_000,
                ArtifactRoots = LocalGenerationStorage.Categories.ToDictionary(category => category, category => new[] { Path.Combine(root, category) })
            };
            foreach (var directory in options.ArtifactRoots.Values.SelectMany(value => value)) Directory.CreateDirectory(directory);
            var registrations = new ServiceCollection().AddLogging().AddApplication().AddDataAccess(config).AddInfrastructure(config).AddPresentation(config);
            registrations.AddSingleton(options);
            registrations.AddSingleton(new DiscoveryRuntimeOptions { RunJobs = true });
            registrations.AddSingleton(new CoordinatedStorageOptions { FilesystemPaths = [root] });
            registrations.AddDbContext<DataContext>(builder => builder.AddInterceptors(new Commands()));
            services = registrations.BuildServiceProvider();
            var users = new Guid[8];
            await using (var initial = services.CreateAsyncScope())
            {
                var provider = initial.ServiceProvider;
                await provider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
                var db = provider.GetRequiredService<DataContext>();
                for (int index = 0; index < users.Length; index++)
                {
                    string email = $"fixture-{index}@example.test";
                    users[index] = await provider.GetRequiredService<IAuthService>().RegisterAsync($"fixture{index}", email, Password);
                    db.Users.Add(new User(email, "UTC", DigestDeliveryDay.Sunday, 8, users[index]));
                }
                await db.SaveChangesAsync();
                await provider.GetRequiredService<LocalGenerationStorage>().ReconcileAsync(await provider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync());
            }
            // Populate fixed modest export histories before writing the large filesystem fixture.
            for (int user = 6; user < 8; user++)
                for (int history = 0; history < 10; history++) await Persist(services, users[user], 500 + history);
            if (Environment.GetEnvironmentVariable("LINER_BENCH_QUERY_FILE") is { } queryFile)
            {
                await QueryCosts(services, queryFile);
                return;
            }
            byte[] buffer = new byte[1024 * 1024]; new Random(42).NextBytes(buffer);
            for (int index = 0; index < files; index++)
            {
                await using var file = File.Create(Path.Combine(options.ArtifactRoots["reports"][0], $"{index:D5}.bin"));
                long remaining = bytes / files;
                while (remaining > 0)
                {
                    int length = (int)Math.Min(buffer.Length, remaining);
                    await file.WriteAsync(buffer.AsMemory(0, length)); remaining -= length;
                }
                file.Flush(true);
            }
            await using (var establish = services.CreateAsyncScope())
                await establish.ServiceProvider.GetRequiredService<LocalGenerationStorage>().ReconcileAsync(
                    await establish.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync());
            if (Environment.GetEnvironmentVariable("LINER_BENCH_SERVE_URL") is { } serveUrl)
            {
                await ServeDisposable(config, options, root, serveUrl);
                return;
            }
            // Warm compilation, connections and the page cache; these are not cold-disk/provider measurements.
            await Login(services, 0);
            await Persist(services, users[4], 900);
            await Export(services, users[6]);
            foreach (string scenario in new[] { "login", "persistence", "mixed-eight" })
            {
                var observations = new ConcurrentBag<Sample>();
                long allocated = GC.GetTotalAllocatedBytes(true), logicalRead = IO("rchar"), physicalRead = IO("read_bytes");
                TimeSpan cpu = Process.GetCurrentProcess().TotalProcessorTime;
                var elapsed = Stopwatch.StartNew();
                if (scenario == "mixed-eight")
                {
                    int waves = (int)Math.Ceiling(samples / 8d);
                    for (int wave = 0; wave < waves; wave++)
                        await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Observe(index < 4 ? "login" : index < 6 ? "persistence" : "export",
                            index, 1000 + wave * 8 + index)));
                }
                else for (int sample = 0; sample < samples; sample++) await Observe(scenario, scenario == "login" ? 0 : 4, 100 + sample);
                elapsed.Stop();
                var raw = observations.OrderBy(sample => sample.Index).Select(sample => sample.Snapshot()).ToArray();
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    shape, artifactBytes = bytes, files, scenario, count = raw.Length,
                    elapsedMs = elapsed.Elapsed.TotalMilliseconds,
                    cpuMs = (Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalMilliseconds,
                    allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated,
                    logicalReadBytes = IO("rchar") - logicalRead, physicalReadBytes = IO("read_bytes") - physicalRead,
                    peakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64,
                    runtime = Environment.Version.ToString(), serverGc = GCSettings.IsServerGC,
                    processors = Environment.ProcessorCount, upstreamRequests = 0,
                    fixtureScoresSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
                        Picks(IsoWeek.FromDate(new DateTime(2026, 10, 1))).Select((pick, rank) => new
                        { Rank = rank + 1, pick.Track.Id, pick.Track.Title, pick.Track.ArtistName, pick.Track.Mbid, pick.Breakdown })))),
                    samples = raw
                }));
                async Task Observe(string kind, int user, int index)
                {
                    var observation = new Sample(kind, index); Current.Value = observation;
                    var timer = Stopwatch.StartNew();
                    try
                    {
                        if (kind == "login") await Login(services, user);
                        else if (kind == "persistence") await Persist(services, users[user], index);
                        else await Export(services, users[user]);
                    }
                    catch (GenerationStoppedException failure)
                    {
                        observation.Failure = failure.Message;
                        observation.FailureSite = string.Join(" > ", new StackTrace(failure, false).GetFrames().Take(4)
                            .Select(frame => frame.GetMethod()?.DeclaringType?.Name + "." + frame.GetMethod()?.Name));
                    }
                    finally { observation.DurationMs = timer.Elapsed.TotalMilliseconds; Current.Value = null; }
                    observations.Add(observation);
                }
            }
        }
        finally
        {
            if (services is not null) await services.DisposeAsync();
            if (created)
            {
                await using var pool = new NpgsqlConnection(settings.ConnectionString);
                NpgsqlConnection.ClearPool(pool);
                await using var db = new NpgsqlConnection(admin); await db.OpenAsync();
                await new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", db).ExecuteNonQueryAsync();
            }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed record QueryVariant(string Name, string Sql);
    private static async Task QueryCosts(ServiceProvider services, string file)
    {
        var variants = JsonSerializer.Deserialize<QueryVariant[]>(await File.ReadAllTextAsync(file))!;
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        await db.Database.OpenConnectionAsync();
        var connection = db.Database.GetDbConnection();
        var timings = variants.ToDictionary(variant => variant.Name, _ => new List<double>());
        string? expectedCounters = null;
        for (int sample = -1; sample < 20; sample++)
            foreach (var variant in variants)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = variant.Sql;
                var timer = Stopwatch.StartNew();
                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) throw new InvalidOperationException("Inventory query rejected fixture.");
                string counters = string.Join(":", new[] { 1, 2, 3, 4, 6 }.Select(index => reader.GetInt64(index)));
                expectedCounters ??= counters;
                if (expectedCounters != counters) throw new InvalidOperationException("Inventory counters changed between query variants.");
                if (sample >= 0) timings[variant.Name].Add(timer.Elapsed.TotalMilliseconds);
            }
        foreach (var variant in variants)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXPLAIN (ANALYZE,BUFFERS,FORMAT JSON) " + variant.Sql;
            var plan = (string)(await command.ExecuteScalarAsync())!;
            Console.WriteLine(JsonSerializer.Serialize(new { query = variant.Name, samplesMs = timings[variant.Name], plan = JsonSerializer.Deserialize<JsonElement>(plan) }));
        }
    }

    private static async Task Login(ServiceProvider services, int user)
    {
        await using var scope = services.CreateAsyncScope(); var provider = scope.ServiceProvider;
        var controller = new AuthController(provider.GetRequiredService<IAuthService>(), provider.GetRequiredService<IJwtService>(),
            Microsoft.Extensions.Options.Options.Create(new JwtOptions()), provider.GetRequiredService<IUnitOfWork>());
        var http = new DefaultHttpContext { RequestServices = provider }; http.Request.Method = "POST";
        var context = new ActionExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), [],
            new Dictionary<string, object?> { ["request"] = new LoginRequest($"fixture-{user}@example.test", Password) }, controller);
        await provider.GetRequiredService<DiscoveryWriteFilter>().OnActionExecutionAsync(context, async () =>
        {
            var identity = await provider.GetRequiredService<IAuthService>().CheckPasswordAsync($"fixture-{user}@example.test", Password)
                ?? throw new InvalidOperationException("Fixture login failed.");
            await provider.GetRequiredService<IAuthService>().StoreRefreshTokenAsync(identity.UserId, "fixture-refresh-" + Guid.NewGuid().ToString("N"));
            return new ActionExecutedContext(context, [], controller) { Result = new OkResult() };
        });
    }

    private static async Task Persist(ServiceProvider services, Guid user, int index)
    {
        await using var scope = services.CreateAsyncScope(); var provider = scope.ServiceProvider;
        var store = provider.GetRequiredService<IDigestGenerationStore>();
        var inputs = await store.ReadInputsAsync(user);
        var week = IsoWeek.FromDate(new DateTime(2020, 1, 6).AddDays(index * 7));
        var picks = Picks(week);
        await provider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(131_072,
            ct => store.PersistWithinAdmissionAsync(user, week, inputs.Revision, picks, ct), default);
    }
    private static async Task Export(ServiceProvider services, Guid user)
    {
        await using var scope = services.CreateAsyncScope();
        var export = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetUserDataExportQuery(user));
        if (export.Digests.Count != 10) throw new InvalidOperationException("Export fixture count changed.");
        await JsonSerializer.SerializeAsync(Stream.Null, export);
    }
    private static IReadOnlyList<BaselineAPick> Picks(IsoWeek week)
    {
        var vector = WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["fixture"] = 1 });
        var evidence = new ScoreEvidence([], [], [], null, [],
            [new StoredGap("fixture", "storage-benchmark", "synthetic-measurement-only", null)], "fixture", "not-measured");
        var scorer = new BaselineAScorer();
        return Enumerable.Range(1, 5).Select(index =>
        {
            var track = new Track("Fixture " + index, "Fixture artist " + index,
                id: Guid.Parse("00000000-0000-0000-0000-" + index.ToString("D12")));
            return new BaselineAPick(track, scorer.Score(new(track, vector, evidence), vector, false, week, new("storage-probe-v1")));
        }).ToArray();
    }
    private static long IO(string name) => OperatingSystem.IsLinux()
        ? long.Parse(File.ReadLines("/proc/self/io").Single(line => line.StartsWith(name + ":", StringComparison.Ordinal)).Split(':')[1].Trim()) : 0;

    private static async Task ServeDisposable(IConfiguration configuration, GenerationStorageOptions storage, string root, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Scheme != "http" ||
            address.Host != "127.0.0.1" || address.Port < 1024)
            throw new ArgumentException("Disposable browser host requires a loopback HTTP port above 1023.");
        var settings = configuration.AsEnumerable().Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value!);
        settings["urls"] = url;
        settings["environment"] = "Development";
        settings["applicationName"] = typeof(AuthController).Assembly.GetName().Name!;
        settings["contentRoot"] = Path.GetFullPath("src/Presentation");
        settings["DiscoveryRuntime:RunJobs"] = "false";
        settings["DiscoveryRuntime:EnforceStorageAdmission"] = "true";
        settings["GuestDiscovery:Enabled"] = "true";
        settings["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1";
        settings["GenerationStorage:InventoryPath"] = storage.InventoryPath!;
        settings["GenerationStorage:LeasePath"] = storage.LeasePath!;
        settings["GenerationStorage:DatabaseOverheadBytesPerPick"] = storage.DatabaseOverheadBytesPerPick.ToString();
        settings["DiscoveryStorage:FilesystemPaths:0"] = root;
        foreach (var category in storage.ArtifactRoots)
            for (int index = 0; index < category.Value.Length; index++)
                settings[$"GenerationStorage:ArtifactRoots:{category.Key}:{index}"] = category.Value[index];
        if (Environment.GetEnvironmentVariable("LINER_BENCH_VERIFY_OPERATOR") == "true")
        {
            string privatePath = Path.Combine(root, "operator-private.json");
            try
            {
                await using (var file = new FileStream(privatePath, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                    UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
                })) await JsonSerializer.SerializeAsync(file, settings);
                var start = new ProcessStartInfo(Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"))
                { UseShellExecute = false };
                foreach (string argument in new[] { "run", "--file", "scripts/reconcile_managed_storage.cs", "--no-cache", "--",
                    "--establish", "--writers-stopped", "--configuration", privatePath }) start.ArgumentList.Add(argument);
                using var verify = Process.Start(start)!;
                await verify.WaitForExitAsync();
                if (verify.ExitCode != 0) throw new InvalidOperationException("Operator establishment verification failed.");
            }
            finally { File.Delete(privatePath); }
        }
        // Use the real composition root in-process; secrets never enter a command line or output.
        var arguments = settings.SelectMany(pair => new[] { "--" + pair.Key, pair.Value }).ToArray();
        var entry = typeof(AuthController).Assembly.EntryPoint!;
        if (entry.Invoke(null, [arguments]) is Task host) await host;
    }

    private sealed class Sample(string kind, int index)
    {
        public readonly string Kind = kind;
        public readonly int Index = index;
        public double DurationMs;
        public string? Failure;
        public string? FailureSite;
        public long Commands, HashBytes, Scans;
        private readonly ConcurrentDictionary<string, List<double>> stages = new(StringComparer.Ordinal);
        public void Record(string stage, double value)
        {
            var values = stages.GetOrAdd(stage, _ => []); lock (values) values.Add(value);
        }
        public object Snapshot() => new
        {
            kind = Kind, index = Index, durationMs = DurationMs, commands = Commands + (stages.TryGetValue("storage.database_inventory", out var reads) ? reads.Count : 0),
            hashBytes = HashBytes, scans = Scans, failure = Failure, failureSite = FailureSite,
            stages = stages.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray())
        };
    }
    private sealed class Commands : DbCommandInterceptor
    {
        private static void Count() { if (Current.Value is { } sample) Interlocked.Increment(ref sample.Commands); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        { Count(); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        { Count(); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<object> result, CancellationToken ct = default)
        { Count(); return ValueTask.FromResult(result); }
    }
}
