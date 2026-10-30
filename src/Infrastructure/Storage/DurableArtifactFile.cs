using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Infrastructure.Storage;

/// <summary>Linux same-directory operations anchored to a directory opened without symlinks.</summary>
internal static class DurableArtifactFile
{
    public static async Task StageAsync(string root, string name, ReadOnlyMemory<byte> content, CancellationToken ct, IArtifactStagingWriter? writer = null)
    {
        using var parent = DurableInventoryFile.OpenDirectory(root);
        int descriptor = OpenAt(parent, name, 1 | 0x40 | 0x80 | 0x20000 | 0x80000, 0x180);
        if (descriptor < 0) throw new IOException("artifact_stage_create_failed");
        await using (var output = new FileStream(new SafeFileHandle((IntPtr)descriptor, true), FileAccess.Write, 65536, false))
        {
            if (writer is null) await output.WriteAsync(content, ct);
            else await writer.WriteAsync(output, content, ct);
            await output.FlushAsync(ct);
            output.Flush(true);
        }
        Flush(parent);
        CheckParent(root, parent);
    }

    public static void Publish(string root, string stage, string final)
    {
        using var parent = DurableInventoryFile.OpenDirectory(root);
        // A retry must inspect and verify the recorded final name before choosing this operation.
        if (RenameNoReplace(parent, stage, parent, final, 1) != 0) throw new IOException("artifact_publish_rename_failed");
        Flush(parent);
        CheckParent(root, parent);
    }

    public static void Delete(string root, string name)
    {
        using var parent = DurableInventoryFile.OpenDirectory(root);
        if (UnlinkAt(parent, name, 0) != 0 && Marshal.GetLastPInvokeError() != 2)
            throw new IOException("artifact_delete_failed");
        Flush(parent);
        CheckParent(root, parent);
    }

    public static void DeleteMany(string root, IReadOnlyList<string> names)
    {
        using var parent = DurableInventoryFile.OpenDirectory(root);
        foreach (string name in names)
            if (UnlinkAt(parent, name, 0) != 0 && Marshal.GetLastPInvokeError() != 2)
                throw new IOException("artifact_delete_failed");
        Flush(parent);
        CheckParent(root, parent);
    }

    public static FileStream OpenRead(string path)
    {
        using var parent = DurableInventoryFile.OpenDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        int descriptor = OpenAt(parent, Path.GetFileName(path), 0x20000 | 0x80000 | 0x800, 0);
        if (descriptor < 0) throw new IOException("artifact_open_failed");
        var handle = new SafeFileHandle((IntPtr)descriptor, true);
        try
        {
            if (Stat(handle, "", 0x1000, 0x301, out var identity) != 0 || (identity.Mask & 0x301) != 0x301 ||
                (identity.Mode & 0xf000) != 0x8000)
                throw new GenerationStoppedException("artifact_not_regular_file");
            if (Flock(handle, 1 | 4) != 0) throw new IOException("artifact_read_busy");
            return new FileStream(handle, FileAccess.Read, 65536, false);
        }
        catch { handle.Dispose(); throw; }
    }

    public static FileStream OpenOwnership(string path, bool shared = false, bool create = true)
    {
        if (!OperatingSystem.IsLinux()) throw new GenerationStoppedException("artifact_durable_publication_unavailable");
        using var parent = DurableInventoryFile.OpenDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        int descriptor = OpenAt(parent, Path.GetFileName(path), (shared ? 0 : 2) | (create ? 0x40 : 0) | 0x20000 | 0x80000 | 0x800, 0x180);
        if (descriptor < 0) throw new IOException("artifact_ownership_open_failed");
        var handle = new SafeFileHandle((IntPtr)descriptor, true);
        try
        {
            if (Stat(handle, "", 0x1000, 0x301, out var identity) != 0 || (identity.Mask & 0x301) != 0x301 ||
                (identity.Mode & 0xf000) != 0x8000)
                throw new GenerationStoppedException("artifact_ownership_invalid");
            if (Flock(handle, (shared ? 1 : 2) | 4) != 0) throw new IOException("artifact_ownership_busy");
            var stream = new FileStream(handle, shared ? FileAccess.Read : FileAccess.ReadWrite, 1, false);
            if (stream.Length != 0) { stream.Dispose(); throw new GenerationStoppedException("artifact_ownership_invalid"); }
            CheckParent(Path.GetDirectoryName(Path.GetFullPath(path))!, parent);
            return stream;
        }
        catch { handle.Dispose(); throw; }
    }

    internal static void RequireRegular(SafeFileHandle handle)
    {
        if (Stat(handle, "", 0x1000, 0x301, out var identity) != 0 || (identity.Mask & 0x301) != 0x301 ||
            (identity.Mode & 0xf000) != 0x8000) throw new GenerationStoppedException("artifact_not_regular_file");
    }

    private static void Flush(SafeFileHandle parent)
    {
        if (Fsync(parent) != 0) throw new IOException("artifact_directory_flush_failed");
    }
    private static void CheckParent(string root, SafeFileHandle parent)
    {
        using var current = DurableInventoryFile.OpenDirectory(root);
        if (Stat(parent, "", 0x1000, 0x101, out var before) != 0 || Stat(current, "", 0x1000, 0x101, out var after) != 0 ||
            (before.Mask & 0x101) != 0x101 || (after.Mask & 0x101) != 0x101 ||
            before.Inode != after.Inode || before.DeviceMajor != after.DeviceMajor || before.DeviceMinor != after.DeviceMinor)
            throw new IOException("artifact_parent_changed");
    }
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct Identity
    {
        [FieldOffset(0)] public uint Mask;
        [FieldOffset(28)] public ushort Mode;
        [FieldOffset(32)] public ulong Inode;
        [FieldOffset(136)] public uint DeviceMajor;
        [FieldOffset(140)] public uint DeviceMinor;
    }
    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Stat(SafeFileHandle handle, string path, int flags, uint mask, out Identity identity);
    [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
    private static extern int OpenAt(SafeFileHandle parent, string path, int flags, uint mode);
    [DllImport("libc", EntryPoint = "renameat2", SetLastError = true)]
    private static extern int RenameNoReplace(SafeFileHandle source, string sourceName, SafeFileHandle target, string targetName, uint flags);
    [DllImport("libc", EntryPoint = "unlinkat", SetLastError = true)]
    private static extern int UnlinkAt(SafeFileHandle parent, string path, int flags);
    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern int Flock(SafeFileHandle handle, int operation);
    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int Fsync(SafeFileHandle handle);
}
