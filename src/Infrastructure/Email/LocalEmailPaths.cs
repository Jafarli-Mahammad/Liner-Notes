using System.Diagnostics;
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
