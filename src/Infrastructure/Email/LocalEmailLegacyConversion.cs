using System.Security.Cryptography;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.Storage;

namespace LinerNotes.Infrastructure.Email;

/// <summary>Explicit, bounded conversion of one authenticated legacy receipt under the artifact lease.</summary>
public sealed class LocalEmailLegacyConversion(LocalEmailOptions options, GenerationStorageOptions storageOptions,
    ILocalEmailStorage storage, LocalEmailMessageSerializer serializer)
{
    public async Task<string> ConvertAsync(Guid userId, Guid digestId, GenerationStorageState database, CancellationToken ct)
    {
        if (userId == Guid.Empty || digestId == Guid.Empty) throw new GenerationStoppedException("invalid_user_or_digest");
        string root = LocalEmailPaths.Root(options, storageOptions);
        string source = Path.Combine(root, $"{digestId:N}.eml");
        string final = source + ".enc";
        if (!File.Exists(source))
        {
            await using var recovery = await storage.AcquireEmailAsync(database, 0, [], true, ct);
            if (File.Exists(final))
            {
                var existing = serializer.ParseStoredCopy(await LocalEmailPaths.ReadAsync(final, options.StoredMessageByteLimit, ct));
                CheckOwnership(existing, userId, digestId);
                await RemoveLegacyPartialsAsync(root, existing, ct);
            }
            else
            {
                var partials = Directory.EnumerateFiles(root, $"{digestId:N}.*.partial").Take(10_001).ToArray();
                if (partials.Length > 10_000) throw new GenerationStoppedException("archive_scan_limit");
                foreach (string partial in partials)
                {
                    byte[] bytes = await LocalEmailPaths.ReadAsync(partial, options.StoredMessageByteLimit, ct);
                    try
                    {
                        if (!LocalEmailMessageSerializer.IsEncrypted(bytes))
                            throw new GenerationStoppedException("email_plaintext_partial_requires_operator_cleanup");
                    }
                    finally { CryptographicOperations.ZeroMemory(bytes); }
                }
            }
            return "no_plaintext_copy";
        }
        byte[] plaintext = await LocalEmailPaths.ReadAsync(source, options.MessageByteLimit, ct);
        try
        {
            var original = serializer.ParseLegacyStoredCopy(plaintext);
            CheckOwnership(original, userId, digestId);
            bool exists = File.Exists(final);
            string[] paths = [final, .. Enumerable.Range(0, 3)
                .Select(_ => Path.Combine(root, $"{digestId:N}.{Guid.NewGuid():N}.partial"))];
            byte[]? ciphertext = exists ? null : serializer.EncryptLegacyStoredCopy(plaintext);
            await using var lease = await storage.AcquireEmailAsync(database, ciphertext?.Length ?? 0, paths, exists, ct);
            if (!exists)
            {
                await lease.ValidateAsync(database, ct);
                LocalEmailPaths.CheckAncestors(paths[1]);
                var fileOptions = new FileStreamOptions
                {
                    Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                    Options = FileOptions.Asynchronous | FileOptions.WriteThrough
                };
                if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                await using (var file = new FileStream(paths[1], fileOptions))
                {
                    await file.WriteAsync(ciphertext!, ct);
                    await file.FlushAsync(ct);
                    file.Flush(true);
                }
                await lease.RecordOwnWriteAsync(paths[1], ct);
                await lease.ValidateAsync(database, ct);
                if (!(await LocalEmailPaths.ReadAsync(paths[1], options.StoredMessageByteLimit, ct)).AsSpan().SequenceEqual(ciphertext))
                    throw new GenerationStoppedException("email_partial_conflict");
                LocalEmailPaths.CheckAncestors(final);
                ct.ThrowIfCancellationRequested();
                File.Move(paths[1], final, overwrite: false);
            }

            // If a previous conversion published then stopped before deletion, verify the same copy before resuming.
            var converted = serializer.ParseStoredCopy(await LocalEmailPaths.ReadAsync(final, options.StoredMessageByteLimit, ct));
            if (converted.Receipt != original.Receipt || converted.PlainText != original.PlainText || converted.Html != original.Html)
                throw new GenerationStoppedException("email_receipt_conflict");
            byte[] current = await LocalEmailPaths.ReadAsync(source, options.MessageByteLimit, ct);
            try
            {
                if (!current.AsSpan().SequenceEqual(plaintext)) throw new GenerationStoppedException("email_inputs_changed");
            }
            finally { CryptographicOperations.ZeroMemory(current); }
            ct.ThrowIfCancellationRequested();
            LocalEmailPaths.CheckAncestors(source);
            File.Delete(source);
            await RemoveLegacyPartialsAsync(root, converted, ct);
            return exists ? "conversion_resumed" : "converted";
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private static void CheckOwnership(ParsedLocalEmail parsed, Guid userId, Guid digestId)
    {
        if (parsed.Receipt.UserId != userId || parsed.Receipt.DigestId != digestId)
            throw new GenerationStoppedException("email_copy_ownership_conflict");
    }

    private async Task RemoveLegacyPartialsAsync(string root, ParsedLocalEmail final, CancellationToken ct)
    {
        var partials = Directory.EnumerateFiles(root, $"{final.Receipt.DigestId:N}.*.partial").Take(10_001).ToArray();
        if (partials.Length > 10_000) throw new GenerationStoppedException("archive_scan_limit");
        foreach (string partial in partials)
        {
            byte[] bytes = await LocalEmailPaths.ReadAsync(partial, options.StoredMessageByteLimit, ct);
            try
            {
                if (LocalEmailMessageSerializer.IsEncrypted(bytes)) continue;
                ParsedLocalEmail old;
                try { old = serializer.ParseLegacyStoredCopy(bytes); }
                catch (GenerationStoppedException) { throw new GenerationStoppedException("email_plaintext_partial_requires_operator_cleanup"); }
                if (old.Receipt != final.Receipt || old.PlainText != final.PlainText || old.Html != final.Html)
                    throw new GenerationStoppedException("email_copy_ownership_conflict");
                ct.ThrowIfCancellationRequested();
                LocalEmailPaths.CheckAncestors(partial);
                File.Delete(partial);
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
    }
}
