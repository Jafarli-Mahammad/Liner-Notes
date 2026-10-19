namespace LinerNotes.Application.Common.Interfaces.Repositories;

public interface IListenerProfileRepository
{
    /// <summary>Lock the account profile, advance its input revision and cancel unfinished generation in the caller's transaction.</summary>
    Task ChangeInputsAsync(Guid userId, CancellationToken ct);
}
