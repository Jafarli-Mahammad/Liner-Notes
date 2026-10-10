using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Domain.Enums;
using LinerNotes.Infrastructure.Storage;

namespace LinerNotes.Infrastructure.Email;

public interface ILocalEmailBodyWriter
{
    Task WriteAsync(Stream stream, ReadOnlyMemory<byte> bytes, CancellationToken ct);
}

public sealed class LocalEmailSink(LocalEmailOptions options, GenerationStorageOptions storageOptions,
    ILocalEmailStorage storage, IDigestEmailRenderer renderer, LocalEmailMessageSerializer serializer,
    ILocalEmailBodyWriter? writer = null) : ILocalEmailSink
{
    public async Task<ILocalEmailCapture> OpenAsync(WeeklyDigestDto digest, SubscriberDto subscriber,
        GenerationStorageState database, CancellationToken ct)
    {
        string root = LocalEmailPaths.Root(options, storageOptions);
        string final = Path.Combine(root, digest.Id.ToString("N") + ".eml");
        string[] paths = [final, ..Enumerable.Range(0,3).Select(_ => Path.Combine(root, digest.Id.ToString("N") + "." + Guid.NewGuid().ToString("N") + ".partial"))];
        bool exists = File.Exists(final);
        if (!exists && digest.Status == DigestStatus.LocalCaptured) throw new GenerationStoppedException("email_receipt_missing");
        RenderedDigestEmail message;
        byte[]? bytes = null;
        if (exists)
        {
            var parsed = serializer.Parse(await LocalEmailPaths.ReadAsync(final, options.MessageByteLimit, ct));
            message = renderer.Render(digest, subscriber, parsed.Receipt.Token);
            Validate(parsed, message);
        }
        else { message = renderer.Render(digest, subscriber); bytes = serializer.Serialize(message); }
        var lease = await storage.AcquireEmailAsync(database, bytes?.Length ?? 0, paths, exists, ct);
        return new Capture(options, paths, bytes, message, renderer, serializer, lease, exists, writer);
    }

    private static void Validate(ParsedLocalEmail parsed, RenderedDigestEmail expected)
    {
        if (parsed.Receipt.DigestId != expected.DigestId || parsed.Receipt.UserId != expected.UserId || parsed.Receipt.Week != expected.Week ||
            parsed.Receipt.Fingerprint != expected.Fingerprint || parsed.PlainText != expected.PlainText || parsed.Html != expected.Html)
            throw new GenerationStoppedException("email_receipt_conflict");
    }

    private sealed class Capture(LocalEmailOptions options, string[] paths, byte[]? bytes, RenderedDigestEmail message,
        IDigestEmailRenderer renderer, LocalEmailMessageSerializer serializer, ILocalEmailWriteLease lease, bool exists,
        ILocalEmailBodyWriter? writer) : ILocalEmailCapture
    {
        public bool ReceiptExists => exists;
        public async Task PublishAsync(WeeklyDigestDto digest, SubscriberDto subscriber, GenerationStorageState database, CancellationToken ct)
        {
            var fresh = renderer.Render(digest, subscriber, message.Token);
            if (fresh.Fingerprint != message.Fingerprint) throw new GenerationStoppedException("email_inputs_changed");
            if (subscriber.EmailUnsubscribedAtUtc is not null) throw new GenerationStoppedException("email_unsubscribed");
            if (exists)
            {
                Validate(serializer.Parse(await LocalEmailPaths.ReadAsync(paths[0], options.MessageByteLimit, ct)), fresh);
                return;
            }
            for (int attempt=0; attempt<3; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                bool created = false;
                try
                {
                    await lease.ValidateAsync(database, ct);
                    LocalEmailPaths.CheckAncestors(paths[attempt+1]);
                    var fileOptions = new FileStreamOptions { Mode=FileMode.CreateNew, Access=FileAccess.Write, Share=FileShare.None,
                        BufferSize=8192, Options=FileOptions.Asynchronous | FileOptions.WriteThrough };
                    if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                    await using (var file = new FileStream(paths[attempt+1], fileOptions))
                    {
                        created = true;
                        if (writer is null) await file.WriteAsync(bytes!, ct);
                        else await writer.WriteAsync(file, bytes!, ct);
                        await file.FlushAsync(ct);
                        file.Flush(true);
                    }
                    await lease.RecordOwnWriteAsync(paths[attempt+1], ct);
                    // Only our three named partials may change while the artifact lease is held.
                    await lease.ValidateAsync(database, ct);
                    var written = await LocalEmailPaths.ReadAsync(paths[attempt+1], options.MessageByteLimit, ct);
                    if (!written.AsSpan().SequenceEqual(bytes)) throw new GenerationStoppedException("email_partial_conflict");
                    LocalEmailPaths.CheckAncestors(paths[0]);
                    ct.ThrowIfCancellationRequested();
                    File.Move(paths[attempt+1], paths[0], overwrite:false);
                    // A published file remains a receipt even if cancellation/SQL failure follows.
                    return;
                }
                catch (IOException) when (File.Exists(paths[0])) { throw new GenerationStoppedException("email_receipt_conflict"); }
                catch (IOException) when (attempt < 2)
                {
                    if (created) await lease.RecordOwnWriteAsync(paths[attempt+1], ct);
                    await Task.Delay(attempt == 0 ? 250 : 1000, ct);
                }
                catch (IOException) { throw new GenerationStoppedException("email_io_unavailable"); }
                catch (UnauthorizedAccessException) { throw new GenerationStoppedException("email_io_forbidden"); }
            }
        }
        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }
}
