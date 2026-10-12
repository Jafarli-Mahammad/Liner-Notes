using System.Net;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Domain.Enums;
using LinerNotes.Infrastructure.Storage;

namespace LinerNotes.Infrastructure.Email;

public sealed class LocalEmailArchive(LocalEmailOptions options, GenerationStorageOptions storage,
    LocalEmailMessageSerializer serializer) : ILocalEmailArchive
{
    // Called only by the explicit local operator helper, never by public export or an HTTP endpoint.
    /// <summary>Reads an authenticated encrypted copy for the specified owner, retaining the unsubscribe capability for local operator use.</summary>
    /// <exception cref="GenerationStoppedException">Storage checks, receipt validation, or ownership checks fail.</exception>
    /// <remarks>Configuration, file access, and cancellation errors propagate.</remarks>
    public async Task<ParsedLocalEmail> ReadForOperatorAsync(Guid userId, Guid digestId, CancellationToken ct)
    {
        string root = LocalEmailPaths.Root(options, storage);
        var parsed = serializer.ParseStoredCopy(await LocalEmailPaths.ReadAsync(
            Path.Combine(root, $"{digestId:N}.eml.enc"), options.StoredMessageByteLimit, ct));
        if (parsed.Receipt.UserId != userId || parsed.Receipt.DigestId != digestId)
            throw new GenerationStoppedException("email_copy_ownership_conflict");
        return parsed;
    }

    /// <summary>Returns authenticated encrypted copies matching the user's digest history, with unsubscribe capabilities removed from the bodies.</summary>
    /// <remarks>Plaintext copies are reported for conversion. Integrity, file access, and configuration failures become archive issues; cancellation propagates. A disabled archive is incomplete if captured digests are expected.</remarks>
    public async Task<LocalEmailArchiveDto> ReadAsync(Guid userId, IReadOnlyList<WeeklyDigestDto> digests, CancellationToken ct)
    {
        var copies = new List<LocalEmailCopyDto>(); var issues = new List<string>();
        var expected = digests.Where(d => d.Status == DigestStatus.LocalCaptured).Select(d => d.Id).ToHashSet();
        if (!options.Enabled) return new(expected.Count > 0 ? "incomplete" : "disabled", [], expected.Count > 0 ? ["archive_unavailable"] : []);
        try
        {
            string root = LocalEmailPaths.Root(options, storage);
            foreach (var file in Files(root))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var bytes = await LocalEmailPaths.ReadAsync(file, options.StoredMessageByteLimit, ct);
                    if (!LocalEmailMessageSerializer.IsEncrypted(bytes))
                    { issues.Add("copy_plaintext_requires_conversion"); continue; }
                    var parsed = serializer.ParseStoredCopy(bytes);
                    if (parsed.Receipt.UserId != userId) continue;
                    var known = digests.FirstOrDefault(d => d.Id == parsed.Receipt.DigestId);
                    if (known is null || known.Week != parsed.Receipt.Week || Path.GetFileName(file) != $"{parsed.Receipt.DigestId:N}.eml.enc")
                    { issues.Add("copy_history_conflict"); continue; }
                    expected.Remove(known.Id);
                    string Sanitize(string value) => value.Replace(Uri.EscapeDataString(parsed.Receipt.Token), "[capability-removed]", StringComparison.Ordinal)
                        .Replace(WebUtility.HtmlEncode(parsed.Receipt.Token), "[capability-removed]", StringComparison.Ordinal);
                    copies.Add(new(known.Id, known.Week, parsed.Receipt.TemplateVersion, parsed.Receipt.Fingerprint, Sanitize(parsed.PlainText), Sanitize(parsed.Html), parsed.Bytes));
                }
                catch (Exception ex) when (ex is GenerationStoppedException or IOException or UnauthorizedAccessException)
                { issues.Add("copy_integrity_unavailable"); }
            }
            if (expected.Count > 0) issues.Add("captured_copy_missing");
        }
        catch (Exception ex) when (ex is GenerationStoppedException or IOException or UnauthorizedAccessException or InvalidOperationException)
        { issues.Add("archive_unavailable"); }
        return new(issues.Count == 0 ? "complete" : "incomplete", copies, issues.Distinct().ToArray());
    }

    /// <summary>Deletes authenticated encrypted or legacy copies and partials owned by the user while holding the artifact lease.</summary>
    /// <remarks>Unverifiable files are retained. Integrity, file access, and configuration failures become cleanup issues; cancellation propagates. Disabled cleanup returns an incomplete result.</remarks>
    public async Task<LocalEmailCleanupResult> DeleteAsync(Guid userId, CancellationToken ct)
    {
        if (!options.Enabled) return new(false, ["archive_unavailable"]);
        var issues = new List<string>();
        try
        {
            string root = LocalEmailPaths.Root(options, storage);
            // Acquire the same artifact lock without requiring headroom or a fresh creation inventory.
            await using var lease = await new LocalGenerationStorage(storage, TimeProvider.System)
                .AcquireEmailAsync(new(0,"cleanup"), 0, [], true, ct);
            foreach (string file in Files(root, includePartials:true))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var bytes = await LocalEmailPaths.ReadAsync(file, options.StoredMessageByteLimit, ct);
                    var parsed = LocalEmailMessageSerializer.IsEncrypted(bytes)
                        ? serializer.ParseStoredCopy(bytes)
                        : serializer.ParseLegacyStoredCopy(bytes);
                    if (parsed.Receipt.UserId == userId) File.Delete(file);
                }
                catch (Exception ex) when (ex is GenerationStoppedException or IOException or UnauthorizedAccessException)
                { issues.Add("copy_cleanup_integrity_unavailable"); }
            }
        }
        catch (Exception ex) when (ex is GenerationStoppedException or IOException or UnauthorizedAccessException or InvalidOperationException)
        { issues.Add("copy_cleanup_unavailable"); }
        return new(issues.Count == 0, issues.Distinct().ToArray());
    }

    /// <summary>Returns ordinally sorted encrypted and legacy email paths, optionally including partials.</summary>
    /// <exception cref="GenerationStoppedException">The directory contains more than 10,000 entries or any subdirectory.</exception>
    /// <remarks>Directory access errors propagate.</remarks>
    private static IReadOnlyList<string> Files(string root, bool includePartials = false)
    {
        var files = Directory.EnumerateFileSystemEntries(root).Take(10_001).ToArray();
        if (files.Length > 10_000) throw new GenerationStoppedException("archive_scan_limit");
        if (files.Any(Directory.Exists)) throw new GenerationStoppedException("archive_shape_invalid");
        return files.Where(f => f.EndsWith(".eml.enc", StringComparison.Ordinal) || f.EndsWith(".eml", StringComparison.Ordinal) ||
            includePartials && f.EndsWith(".partial", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
    }
}
