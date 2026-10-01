namespace LinerNotes.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when a requested entity cannot be found in the persistence store.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.")
    {
    }
}
