namespace LinerNotes.Infrastructure.Storage;

/// <summary>Bounded staging I/O, replaceable for controlled filesystem-failure tests.</summary>
public interface IArtifactStagingWriter
{
    Task WriteAsync(Stream target, ReadOnlyMemory<byte> content, CancellationToken ct);
}
