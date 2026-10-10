using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.Storage;

namespace LinerNotes.Infrastructure.Email;

internal static class LocalEmailPaths
{
    public static string Root(LocalEmailOptions email, GenerationStorageOptions storage)
    {
        email.Validate();
        string root = email.SinkRoot ?? "";
        if (!Path.IsPathFullyQualified(root) || !Directory.Exists(root)) throw new GenerationStoppedException("email_sink_unconfigured");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!storage.ArtifactRoots.TryGetValue("copies", out var copies) || !copies.Any(c => Path.TrimEndingDirectorySeparator(Path.GetFullPath(c)) == root))
            throw new GenerationStoppedException("email_sink_not_accounted");
        if (OperatingSystem.IsWindows()) ValidateWindowsPermissions(root);
        else
        {
            var mode = File.GetUnixFileMode(root);
            const UnixFileMode sharedAccess = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            const UnixFileMode ownerAccess = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            if ((mode & sharedAccess) != 0 || (mode & ownerAccess) != ownerAccess)
                throw new GenerationStoppedException("email_sink_permissions_invalid");
        }
        CheckAncestors(root);
        for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
        {
            if (directory.Name == ".git") throw new GenerationStoppedException("email_sink_tracked");
            if (!File.Exists(Path.Combine(directory.FullName, ".git")) && !Directory.Exists(Path.Combine(directory.FullName, ".git"))) continue;
            var start = new ProcessStartInfo("git") { WorkingDirectory=directory.FullName, RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false };
            foreach (string arg in new[] { "check-ignore", "-q", "--", Path.Combine(root, "capture.eml") }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new GenerationStoppedException("email_sink_tracked");
            process.WaitForExit();
            if (process.ExitCode != 0) throw new GenerationStoppedException("email_sink_tracked");
            // An ignored path can still contain explicitly tracked files.
            start.ArgumentList.Clear(); foreach (string arg in new[] { "ls-files", "--", root }) start.ArgumentList.Add(arg);
            using var tracked = Process.Start(start) ?? throw new GenerationStoppedException("email_sink_tracked");
            string output = tracked.StandardOutput.ReadToEnd(); tracked.WaitForExit();
            if (tracked.ExitCode != 0 || output.Length != 0) throw new GenerationStoppedException("email_sink_tracked");
            break;
        }
        return root;
    }

    [SupportedOSPlatform("windows")]
    private static void ValidateWindowsPermissions(string root)
    {
        var security = new DirectoryInfo(root).GetAccessControl();
        var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
        var currentUser = WindowsIdentity.GetCurrent().User;
        if (owner is null || currentUser is null || !owner.Equals(currentUser))
            throw new GenerationStoppedException("email_sink_permissions_invalid");

        var trusted = new HashSet<SecurityIdentifier>
        {
            owner,
            new(WellKnownSidType.LocalSystemSid, null),
            new(WellKnownSidType.BuiltinAdministratorsSid, null)
        };
        var accessRules = security.GetAccessRules(includeExplicit: true, includeInherited: true,
            targetType: typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>();
        if (accessRules.Any(rule => rule.AccessControlType == AccessControlType.Allow &&
            rule.IdentityReference is SecurityIdentifier sid && !trusted.Contains(sid)))
            throw new GenerationStoppedException("email_sink_permissions_invalid");
    }

    public static void CheckAncestors(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new GenerationStoppedException("symlink_email_path");
    }

    public static async Task<byte[]> ReadAsync(string file, int bound, CancellationToken ct)
    {
        CheckAncestors(file);
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
        if (stream.Length > bound) throw new GenerationStoppedException("email_receipt_invalid");
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, ct);
        if (stream.Length != bytes.Length) throw new GenerationStoppedException("email_receipt_invalid");
        return bytes;
    }
}
