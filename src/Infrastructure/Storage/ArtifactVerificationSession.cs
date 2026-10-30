using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Infrastructure.Storage;

/// <summary>Operation-local proofs. Only a live kernel-enforced read lease permits reusing a content digest.</summary>
internal sealed class ArtifactVerificationSession(bool useKernelReadLeases = true) : IDisposable
{
    private const int MaximumLeases = 128;
    private static readonly SemaphoreSlim HashSlots = new(Math.Min(2, Environment.ProcessorCount), Math.Min(2, Environment.ProcessorCount));
    private readonly ConcurrentDictionary<string, ContentProof> proofs = new(StringComparer.Ordinal);
    private int pinned;

    public async Task<byte[]> HashAsync(string path, long expectedLength, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (proofs.TryGetValue(path, out var proof))
        {
            proof.Validate();
            if (proof.Lease.Stream.Length != expectedLength) throw new GenerationStoppedException("artifact_changed_during_inventory");
            LinerNotes.Application.Common.Diagnostics.DiscoveryDiagnostics.ContentReused();
            return proof.Hash;
        }
        KernelReadLease? lease = null;
        // Small-file measurements show path-proof syscalls cost more than rereading the file.
        // Keep full verification there; only pin files large enough to amortize the guard.
        if (useKernelReadLeases && expectedLength >= 4 * 1024 * 1024)
        {
            if (Interlocked.Increment(ref pinned) <= MaximumLeases)
            {
                try { lease = KernelReadLease.TryAcquire(path); }
                catch { Interlocked.Decrement(ref pinned); throw; }
                if (lease is null) Interlocked.Decrement(ref pinned);
            }
            else Interlocked.Decrement(ref pinned);
        }
        if (lease is null)
        {
            await using var input = OperatingSystem.IsLinux() ? DurableArtifactFile.OpenRead(path) :
                new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
            if (input.Length != expectedLength) throw new GenerationStoppedException("artifact_changed_during_inventory");
            var hash = await HashFullContentAsync(input, ct).ConfigureAwait(false);
            if (input.Length != expectedLength) throw new GenerationStoppedException("artifact_changed_during_inventory");
            return hash;
        }
        try
        {
            if (lease.Stream.Length != expectedLength) throw new GenerationStoppedException("artifact_changed_during_inventory");
            byte[] hash = await HashFullContentAsync(lease.Stream, ct).ConfigureAwait(false);
            if (lease.Stream.Length != expectedLength) throw new GenerationStoppedException("artifact_changed_during_inventory");
            lease.Validate();
            if (!proofs.TryAdd(path, new(lease, hash))) throw new GenerationStoppedException("artifact_duplicate_scan_path");
            return hash;
        }
        catch { lease.Dispose(); Interlocked.Decrement(ref pinned); throw; }
    }

    internal static async Task<byte[]> HashFullContentAsync(Stream input, CancellationToken ct)
    {
        await HashSlots.WaitAsync(ct).ConfigureAwait(false);
        try { return await HashContentCoreAsync(input, ct).ConfigureAwait(false); }
        finally { HashSlots.Release(); }
    }

    private static async Task<byte[]> HashContentCoreAsync(Stream input, CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(65536);
        long bytesRead = 0;
        try
        {
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, 65536), ct).ConfigureAwait(false)) > 0)
            {
                bytesRead += read;
                hash.AppendData(buffer, 0, read);
            }
            return hash.GetHashAndReset();
        }
        finally
        {
            LinerNotes.Application.Common.Diagnostics.DiscoveryDiagnostics.ContentRead(bytesRead);
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    public void Validate() { foreach (var proof in proofs.Values) proof.Validate(); }
    public void Dispose() { foreach (var proof in proofs.Values) proof.Lease.Dispose(); proofs.Clear(); pinned = 0; }
    private sealed record ContentProof(KernelReadLease Lease, byte[] Hash)
    { public void Validate() => Lease.Validate(); }

    // This is an optional optimization. Failed/unsupported native primitives fall back to full reads.
    // The signal is only a wakeup: F_GETLEASE and descriptor/path identity are the authoritative proof.
    private sealed class KernelReadLease : IDisposable
    {
        private const int ReadLease = 0, Unlock = 2, SetLease = 1024, GetLease = 1025;
        private const int NoFollow = 0x20000, CloseOnExec = 0x80000, DirectoryOnly = 0x10000;
        private static readonly ConcurrentDictionary<int, KernelReadLease> Active = new();
        private static readonly PosixSignalRegistration? Signal = RegisterSignal();
        private readonly string path;
        private readonly NativeStat identity;
        private int broken, installed, disposed;
        private readonly SafeFileHandle handle;
        public FileStream Stream { get; }
        private int Descriptor => handle.DangerousGetHandle().ToInt32();

        private KernelReadLease(string path, FileStream stream, NativeStat identity)
        { this.path = path; Stream = stream; handle = stream.SafeFileHandle; this.identity = identity; }

        public static KernelReadLease? TryAcquire(string path)
        {
            if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64) || Signal is null) return null;
            FileStream? stream = null; KernelReadLease? proof = null; SafeFileHandle? opened = null;
            try
            {
                opened = OpenWithoutSymlinks(path);
                stream = new FileStream(opened, FileAccess.Read, 8192, isAsync: false);
                if (Stat(stream.SafeFileHandle, "", 0x1000, 0x7ff, out var identity) != 0 || (identity.Mask & 0x101) != 0x101)
                { stream.Dispose(); return null; }
                if ((identity.Mode & 0xf000) != 0x8000) throw new GenerationStoppedException("artifact_not_regular_file");
                // A local lease is not proof against writes through another remote filesystem client.
                // Unknown/stacked/network filesystems retain full reads at every checkpoint.
                if (StatFilesystem(stream.SafeFileHandle, out var filesystem) != 0 ||
                    unchecked((uint)filesystem.Type) is not (0xEF53 or 0x58465342 or 0x9123683E or 0x01021994))
                { stream.Dispose(); return null; }
                proof = new(path, stream, identity);
                Active.TryAdd(proof.Descriptor, proof);
                if (Fcntl(stream.SafeFileHandle, SetLease, ReadLease) != 0) { proof.Dispose(); return null; }
                Volatile.Write(ref proof.installed, 1);
                proof.Validate();
                return proof;
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException or PlatformNotSupportedException)
            { if (proof is not null) proof.Dispose(); else if (stream is not null) stream.Dispose(); else opened?.Dispose(); return null; }
            catch { if (proof is not null) proof.Dispose(); else if (stream is not null) stream.Dispose(); else opened?.Dispose(); throw; }
        }

        public void Validate()
        {
            if (Volatile.Read(ref broken) != 0 || Fcntl(handle, GetLease, 0) != ReadLease)
                throw new GenerationStoppedException("artifact_changed_during_verification");
            using var current = OpenWithoutSymlinks(path);
            if (Stat(current, "", 0x1000, 0x7ff, out var actual) != 0 || actual.Inode != identity.Inode ||
                actual.DeviceMajor != identity.DeviceMajor || actual.DeviceMinor != identity.DeviceMinor)
                throw new GenerationStoppedException("artifact_replaced_during_verification");
            if (Volatile.Read(ref broken) != 0 || Fcntl(handle, GetLease, 0) != ReadLease)
                throw new GenerationStoppedException("artifact_changed_during_verification");
        }

        private void ReleaseRequestedLease()
        {
            if (Volatile.Read(ref installed) == 0) return;
            try
            {
                // A break request changes F_GETLEASE's reported target before the writer can modify bytes.
                if (Fcntl(handle, GetLease, 0) == ReadLease) return;
                Volatile.Write(ref broken, 1);
                Fcntl(handle, SetLease, Unlock);
            }
            catch (ObjectDisposedException) { }
        }

        private static PosixSignalRegistration? RegisterSignal()
        {
            if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64)) return null;
            try
            {
                return PosixSignalRegistration.Create((PosixSignal)29, context =>
                {
                    context.Cancel = true;
                    foreach (var proof in Active.Values) proof.ReleaseRequestedLease();
                });
            }
            catch (Exception ex) when (ex is PlatformNotSupportedException or ArgumentException) { return null; }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            Active.TryRemove(Descriptor, out _);
            try { Fcntl(handle, SetLease, Unlock); }
            finally { Stream.Dispose(); }
        }

        // Pin each path component rather than following a parent-directory symlink after a check.
        private static SafeFileHandle OpenWithoutSymlinks(string path)
        {
            if (!Path.IsPathFullyQualified(path)) throw new IOException("artifact_path_invalid");
            SafeFileHandle current = new((IntPtr)Open("/", DirectoryOnly | CloseOnExec), ownsHandle: true);
            try
            {
                string[] components = Path.GetFullPath(path).Split('/', StringSplitOptions.RemoveEmptyEntries);
                for (int index = 0; index < components.Length; index++)
                {
                    int descriptor = OpenAt(current, components[index], NoFollow | CloseOnExec | 0x800 |
                        (index + 1 < components.Length ? DirectoryOnly : 0));
                    if (descriptor < 0) throw new IOException("artifact_path_unavailable");
                    var next = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
                    current.Dispose(); current = next;
                }
                if (current.IsInvalid) throw new IOException("artifact_path_unavailable");
                return current;
            }
            catch { current.Dispose(); throw; }
        }

        [StructLayout(LayoutKind.Explicit, Size = 256)]
        private struct NativeStat
        {
            [FieldOffset(0)] public uint Mask;
            [FieldOffset(28)] public ushort Mode;
            [FieldOffset(32)] public ulong Inode;
            [FieldOffset(136)] public uint DeviceMajor;
            [FieldOffset(140)] public uint DeviceMinor;
        }
        [DllImport("libc", EntryPoint = "open", SetLastError = true)]
        private static extern int Open(string path, int flags);
        [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
        private static extern int OpenAt(SafeFileHandle parent, string path, int flags);
        [StructLayout(LayoutKind.Explicit, Size = 120)]
        private struct NativeFilesystem
        {
            [FieldOffset(0)] public long Type;
        }
        [DllImport("libc", EntryPoint = "fstatfs", SetLastError = true)]
        private static extern int StatFilesystem(SafeFileHandle handle, out NativeFilesystem filesystem);

        [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
        private static extern int Fcntl(SafeFileHandle descriptor, int command, int argument);
        [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
        private static extern int Stat(SafeFileHandle descriptor, string path, int flags, uint mask, out NativeStat stat);
    }
}
