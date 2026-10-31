// Experimental measurement only. Not referenced by any production project.
// Compiled with the remediation Audit harness; the report records the exact workload and limitations.
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using LinerNotes.Infrastructure.Storage;

internal static partial class Audit
{
    static async Task StorageAlternatives()
    {
        foreach (var shape in new[] { (MiB: 100, Files: 1), (MiB: 100, Files: 1000), (MiB: 512, Files: 1) })
        {
            var root = Scratch("proposal-storage-" + Guid.NewGuid().ToString("N"));
            var options = StorageOptions(root);
            try
            {
                byte[] block = new byte[1024 * 1024]; new Random(42).NextBytes(block);
                for (int i = 0; i < shape.Files; i++)
                {
                    await using var file = File.Create(System.IO.Path.Combine(options.ArtifactRoots["cache"][0], $"{i:D5}.bin"));
                    long left = (long)shape.MiB * 1024 * 1024 / shape.Files;
                    while (left > 0) { int count = (int)Math.Min(block.Length, left); await file.WriteAsync(block.AsMemory(0, count)); left -= count; }
                    file.Flush(true);
                }
                var production = new LocalGenerationStorage(options, TimeProvider.System);
                var measure = typeof(LocalGenerationStorage).GetMethod("MeasureAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                async Task<Dictionary<string, ArtifactMeasurement>> Existing() =>
                    await (Task<Dictionary<string, ArtifactMeasurement>>)measure.Invoke(production, [CancellationToken.None, null])!;
                var expected = await Existing();
                var samples = new Dictionary<string, List<object>>();
                // Rotate order within each sample to reduce systematic temperature/cache bias. All scans read every byte.
                for (int sample = -1; sample < 20; sample++)
                {
                    var modes = new[] { "production", "buffer64k", "buffer64k_parallel2" };
                    foreach (string mode in modes.Skip((sample + 1) % 3).Concat(modes.Take((sample + 1) % 3)))
                    {
                        long allocation = GC.GetTotalAllocatedBytes(true), read = IO("rchar");
                        var timer = Stopwatch.StartNew();
                        for (int checkpoint = 0; checkpoint < 5; checkpoint++)
                        {
                            var actual = mode == "production" ? await Existing() : await ScanPrototype(options, mode.EndsWith("parallel2") ? 2 : 1);
                            if (expected.Any(pair => actual[pair.Key] != pair.Value)) throw new Exception("prototype_revision_mismatch");
                        }
                        timer.Stop();
                        if (sample < 0) continue;
                        if (!samples.TryGetValue(mode, out var values)) samples.Add(mode, values = []);
                        values.Add(new { ms = timer.Elapsed.TotalMilliseconds, readBytes = IO("rchar") - read, allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocation });
                    }
                }
                var target = Directory.GetFiles(options.ArtifactRoots["cache"][0])[0];
                var before = new FileInfo(target); long size = before.Length; var timestamp = before.LastWriteTimeUtc;
                await using (var stream = new FileStream(target, FileMode.Open, FileAccess.ReadWrite))
                { int value = stream.ReadByte(); stream.Position = 0; stream.WriteByte((byte)(value ^ 1)); stream.Flush(true); }
                File.SetLastWriteTimeUtc(target, timestamp);
                var changed = await Existing(); var prototypeChanged = await ScanPrototype(options, 2);
                Emit(new { scenario = "storage-full-read-alternatives", shape.MiB, shape.Files, samples,
                    unchangedMetadataMissesMutation = new FileInfo(target).Length == size && File.GetLastWriteTimeUtc(target) == timestamp,
                    productionDetectsMutation = changed["cache"] != expected["cache"],
                    prototypeDetectsMutation = prototypeChanged["cache"] != expected["cache"],
                    revisionsRemainEqual = changed["cache"] == prototypeChanged["cache"] });
            }
            finally { Directory.Delete(root, recursive: true); }
        }
    }

    // Prototype of the scan primitive only. This is not an admission/publication implementation.
    // Full namespace enumeration, every SHA-256 input byte and existing digest ordering are retained.
    static async Task<Dictionary<string, ArtifactMeasurement>> ScanPrototype(GenerationStorageOptions options, int concurrency)
    {
        var result = new Dictionary<string, ArtifactMeasurement>(StringComparer.Ordinal);
        int fileCount = 0; long sharedBytes = 0;
        foreach (var category in LocalGenerationStorage.Categories)
        {
            long total = 0; using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var root in options.ArtifactRoots[category].Order(StringComparer.Ordinal))
            {
                var files = new List<string>(); var directories = new Stack<string>(); directories.Push(root);
                while (directories.TryPop(out var directory))
                {
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("symlink");
                    foreach (var path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
                    {
                        var attributes = File.GetAttributes(path);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("symlink");
                        if ((attributes & FileAttributes.Directory) != 0) { directories.Push(path); continue; }
                        if (++fileCount > 100_000) throw new IOException("file_limit");
                        files.Add(path);
                    }
                }
                var measured = new (long Length, long Timestamp, byte[] Hash)[files.Count];
                await Parallel.ForEachAsync(Enumerable.Range(0, files.Count), new ParallelOptions { MaxDegreeOfParallelism = concurrency }, async (index, ct) =>
                {
                    var info = new FileInfo(files[index]); long size = info.Length;
                    await using var stream = new FileStream(files[index], FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
                    using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(65536);
                    try
                    {
                        int count; while ((count = await stream.ReadAsync(buffer.AsMemory(0, 65536), ct)) > 0) sha.AppendData(buffer, 0, count);
                    }
                    finally { System.Buffers.ArrayPool<byte>.Shared.Return(buffer); }
                    info.Refresh();
                    if (stream.Length != size || info.Length != size) throw new IOException("changed");
                    measured[index] = (size, info.LastWriteTimeUtc.Ticks, sha.GetHashAndReset());
                });
                for (int index = 0; index < files.Count; index++)
                {
                    var measurement = measured[index]; var file = files[index];
                    total = checked(total + measurement.Length); sharedBytes = checked(sharedBytes + measurement.Length);
                    if (sharedBytes >= LocalGenerationStorage.StopBytes) throw new IOException("headroom");
                    digest.AppendData(Encoding.UTF8.GetBytes($"{file.Length}:{file}:{measurement.Length}:{measurement.Timestamp}:"));
                    digest.AppendData(measurement.Hash);
                }
                digest.AppendData(Encoding.UTF8.GetBytes($"root:{root.Length}:{root}"));
            }
            result.Add(category, new(total, Convert.ToHexStringLower(digest.GetHashAndReset())));
        }
        return result;
    }
}
