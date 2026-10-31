using System.Runtime.InteropServices;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using Microsoft.Win32.SafeHandles;

namespace LinerNotes.Infrastructure.Storage;

/// <summary>Same-directory publication with file and directory durability. All Linux path components are pinned without symlinks.</summary>
internal static class DurableInventoryFile
{
    public static async Task WriteAsync(string path, GenerationInventory inventory, CancellationToken ct, Func<CancellationToken, Task<bool>>? stillCurrent = null)
    {
        if (!OperatingSystem.IsLinux()) throw new GenerationStoppedException("storage_directory_durability_unavailable");
        FileStream? ownership = null;
        for (int attempt = 0; ownership is null; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { ownership = DurableArtifactFile.OpenOwnership(path + ".publication.lock"); }
            catch (IOException) when (attempt < 199) { await Task.Delay(25, ct); }
            catch (IOException) { throw new GenerationStoppedException("storage_metadata_publication_busy"); }
        }
        await using var publication = ownership;
        if (stillCurrent is not null && !await stillCurrent(ct)) throw new GenerationStoppedException("storage_commit_witness_changed");
        string parentPath = Path.GetDirectoryName(Path.GetFullPath(path))!;
        using var parent = OpenDirectory(parentPath);
        string finalName = Path.GetFileName(path);
        string temporary = finalName + "." + (inventory.CommitId ?? Guid.NewGuid()).ToString("N") + ".tmp";
        bool created = false;
        try
        {
            int existing = OpenAt(parent, temporary, 0x20000 | 0x80000 | 0x800, 0);
            if (existing >= 0)
            {
                await using (var input = new FileStream(new SafeFileHandle((IntPtr)existing, true), FileAccess.Read, 4096, false))
                {
                    DurableArtifactFile.RequireRegular(input.SafeFileHandle);
                    if (inventory.CommitId is null || input.Length > 1_000_000)
                        throw new GenerationStoppedException("storage_temporary_inventory_invalid");
                    // The trusted commit owns this exact staging name. A crash can leave any
                    // prefix of its JSON; rebuild it only after the caller verified the full
                    // artifact namespace against that same database witness.

                }
                if (UnlinkAt(parent, temporary, 0) != 0) throw new IOException("storage_temporary_cleanup_failed");
            }
            else if (Marshal.GetLastPInvokeError() != 2) throw new IOException("storage_temporary_inventory_unavailable");
            int descriptor = OpenAt(parent, temporary, 1 | 0x40 | 0x80 | 0x20000 | 0x80000, 0x180);
            if (descriptor < 0) throw new IOException("storage_temporary_create_failed");
            created = true;
            await using (var output = new FileStream(new SafeFileHandle((IntPtr)descriptor, true), FileAccess.Write, 4096, false))
            {
                await JsonSerializer.SerializeAsync(output, inventory, cancellationToken: ct);
                await output.FlushAsync(ct);
                output.Flush(flushToDisk: true);
            }
            ct.ThrowIfCancellationRequested();
            if (RenameAt(parent, temporary, parent, finalName) != 0) throw new IOException("storage_inventory_rename_failed");
            created = false;
            if (Fsync(parent) != 0) throw new IOException("storage_directory_flush_failed");
            using var currentParent = OpenDirectory(parentPath);
            if (Stat(parent, "", 0x1000, 0x101, out var original) != 0 ||
                Stat(currentParent, "", 0x1000, 0x101, out var current) != 0 ||
                (original.Mask & 0x101) != 0x101 || (current.Mask & 0x101) != 0x101 ||
                original.Inode != current.Inode || original.DeviceMajor != current.DeviceMajor || original.DeviceMinor != current.DeviceMinor)
                throw new IOException("storage_inventory_parent_changed");
            if (stillCurrent is not null && !await stillCurrent(ct))
                throw new GenerationStoppedException("storage_commit_witness_changed");
        }
        catch (JsonException) { throw new GenerationStoppedException("storage_temporary_inventory_invalid"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new GenerationStoppedException("committed_inventory_publication_failed"); }
        finally { if (created) UnlinkAt(parent, temporary, 0); }
    }

    internal static SafeFileHandle OpenDirectory(string path)
    {
        var current = new SafeFileHandle((IntPtr)Open("/", 0x10000 | 0x80000), true);
        try
        {
            foreach (string component in Path.GetFullPath(path).Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                int descriptor = OpenAt(current, component, 0x10000 | 0x20000 | 0x80000, 0);
                if (descriptor < 0) throw new GenerationStoppedException("committed_inventory_publication_failed");
                var next = new SafeFileHandle((IntPtr)descriptor, true);
                current.Dispose(); current = next;
            }
            return current;
        }
        catch { current.Dispose(); throw; }
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct DirectoryIdentity
    {
        [FieldOffset(0)] public uint Mask;
        [FieldOffset(32)] public ulong Inode;
        [FieldOffset(136)] public uint DeviceMajor;
        [FieldOffset(140)] public uint DeviceMinor;
    }
    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Stat(SafeFileHandle handle, string path, int flags, uint mask, out DirectoryIdentity identity);
    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(string path, int flags);
    [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
    private static extern int OpenAt(SafeFileHandle parent, string path, int flags, uint mode);
    [DllImport("libc", EntryPoint = "renameat", SetLastError = true)]
    private static extern int RenameAt(SafeFileHandle source, string sourceName, SafeFileHandle destination, string destinationName);
    [DllImport("libc", EntryPoint = "unlinkat", SetLastError = true)]
    private static extern int UnlinkAt(SafeFileHandle parent, string path, int flags);
    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int Fsync(SafeFileHandle descriptor);
}
