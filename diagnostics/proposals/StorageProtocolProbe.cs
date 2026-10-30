using System.Diagnostics;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.Storage;

internal static partial class Audit
{
    static async Task StorageProtocol()
    {
        foreach (var shape in new[] { (Bytes: 0L, Files: 0), (Bytes: 100_000_000L, Files: 1),
            (Bytes: 900_000_000L, Files: 1), (Bytes: 900_000_000L, Files: 1000) })
        {
            var parent = Environment.GetEnvironmentVariable("LINER_STORAGE_PROBE_ROOT") ?? System.IO.Path.GetTempPath();
            var directory = System.IO.Path.Combine(parent, "liner-protocol-probe-" + Guid.NewGuid().ToString("N"));
            var options = StorageOptions(directory);
            bool useKernel = Environment.GetEnvironmentVariable("LINER_PROBE_KERNEL_LEASES") != "false";
            // Reflection permits the identical probe to run against the frozen pre-redesign assembly.
            typeof(GenerationStorageOptions).GetProperty("EnableKernelReadLeases")?.SetValue(options, useKernel);
            var samples = new List<object>();
            try
            {
                byte[] buffer = new byte[1024 * 1024]; new Random(42).NextBytes(buffer);
                for (int index = 0; index < shape.Files; index++)
                {
                    await using var output = File.Create(System.IO.Path.Combine(options.ArtifactRoots["cache"][0], $"{index:D5}.bin"));
                    long left = shape.Bytes / shape.Files;
                    while (left > 0) { int count = (int)Math.Min(left, buffer.Length); await output.WriteAsync(buffer.AsMemory(0, count)); left -= count; }
                    output.Flush(true);
                }
                var storage = new LocalGenerationStorage(options, TimeProvider.System, new() { FilesystemPaths = [directory] });
                var state = new GenerationStorageState(100_000, "managed-v1:synthetic");
                await storage.ReconcileAsync(state);
                for (int sample = -1; sample < 20; sample++)
                {
                    long allocated = GC.GetTotalAllocatedBytes(true), read = IO("rchar"), physical = IO("read_bytes");
                    var cpu = Process.GetCurrentProcess().TotalProcessorTime; var timer = Stopwatch.StartNew();
                    await using (var lease = await storage.AcquireCoordinatedAsync(state, default))
                    {
                        var reservation = await lease.ReserveAsync(100, 1, state);
                        await reservation.ValidateAsync(state);
                        await reservation.VerifyStoredAsync(100, state);
                        await lease.PublishCommittedInventoryAsync(state, default);
                    }
                    timer.Stop();
                    if (sample >= 0) samples.Add(new { ms = timer.Elapsed.TotalMilliseconds, readBytes = IO("rchar") - read,
                        physicalReadBytes = IO("read_bytes") - physical, allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated,
                        cpuMs = (Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalMilliseconds });
                }
                Emit(new { scenario = "coordinated-five-checkpoints", shape.Bytes, shape.Files, useKernel, samples,
                    serverGc = System.Runtime.GCSettings.IsServerGC, processors = Environment.ProcessorCount,
                    peakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64,
                    limitation = "Storage component only; no database mutation or provider I/O. Five validation checkpoints are retained." });
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }
}
