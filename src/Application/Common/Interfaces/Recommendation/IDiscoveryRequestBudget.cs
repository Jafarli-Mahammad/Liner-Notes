namespace LinerNotes.Application.Common.Interfaces.Recommendation;

/// <summary>One acquisition allowance shared by seed and pool work for the current generation.</summary>
public interface IDiscoveryRequestBudget
{
    IDisposable Begin(int maximumRequests);
}
