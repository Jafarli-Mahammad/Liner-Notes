namespace LinerNotes.Domain.Enums;

/// <summary>
/// User sentiment expressed on a specific recommendation.
/// </summary>
public enum UserFeedback
{
    None = 0,
    Liked = 1,
    Disliked = 2,
    AlreadyKnown = 3
}
