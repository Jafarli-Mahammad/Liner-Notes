namespace LinerNotes.Domain.Discovery;

/// <summary>Null means not visited, "skipped" means deliberately cleared.</summary>
public sealed record TasteOnboarding(
    string Version = "taste-onboarding-v1", string Step = "questions",
    string? Goal = null, string? Exploration = null, string? Vocals = null, bool AllStyles = false)
{
    public void Validate()
    {
        if (Version != "taste-onboarding-v1" || Step is not ("questions" or "tags" or "favorites" or "review" or "complete") ||
            Goal is not (null or "skipped" or "new-artists" or "favorites" or "different-styles" or "mixture") ||
            Exploration is not (null or "skipped" or "close" or "balance" or "surprise") ||
            Vocals is not (null or "skipped" or "vocals" or "instrumental" or "both" or "no-preference"))
            throw new ArgumentException("invalid_onboarding_answers");
    }

    public bool ConflictsWith(TasteOnboarding other) => Conflict(Goal, other.Goal) ||
        Conflict(Exploration, other.Exploration) || Conflict(Vocals, other.Vocals);

    public TasteOnboarding FillUnanswered(TasteOnboarding other) => this with
    {
        Goal = Answered(Goal) ? Goal : other.Goal ?? Goal,
        Exploration = Answered(Exploration) ? Exploration : other.Exploration ?? Exploration,
        Vocals = Answered(Vocals) ? Vocals : other.Vocals ?? Vocals,
        AllStyles = AllStyles || other.AllStyles
    };

    private static bool Answered(string? value) => value is not (null or "skipped");
    private static bool Conflict(string? a, string? b) => Answered(a) && Answered(b) && a != b;
}
