using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.Features.Digests.Commands.RecordFeedback;
using LinerNotes.Application.Features.Digests.Queries.GetLatestDigest;
using LinerNotes.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LinerNotes.Presentation.Controllers;

public sealed record RecordFeedbackRequest(
    UserFeedback Feedback,
    string? Comment = null,
    int? Rating = null);

/// <summary>
/// Endpoints for inspecting weekly digests, recommended tracks, and submitting 1-click feedback.
/// </summary>
[Authorize]
public sealed class DigestsController : ApiControllerBase
{
    /// <summary>
    /// Retrieves the most recent weekly digest generated for the current subscriber.
    /// </summary>
    [HttpGet("latest")]
    [ProducesResponseType(typeof(WeeklyDigestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestDigest(CancellationToken cancellationToken)
    {
        var digest = await Mediator.Send(new GetLatestDigestQuery(CurrentUser.UserId), cancellationToken);
        if (digest is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "No Digest Found",
                Detail = "No weekly discovery digest has been generated for your profile yet.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(digest);
    }

    /// <summary>
    /// Records 1-click feedback (Liked, Disliked, AlreadyKnown, or 1-10 granular rating) on a recommended track.
    /// </summary>
    [HttpPost("recommendations/{recommendationId:guid}/feedback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordFeedback(
        [FromRoute] Guid recommendationId,
        [FromBody] RecordFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RecordRecommendationFeedbackCommand(
            RecommendationId: recommendationId,
            UserId: CurrentUser.UserId,
            Feedback: request.Feedback,
            Comment: request.Comment,
            Rating: request.Rating);

        var succeeded = await Mediator.Send(command, cancellationToken);
        if (!succeeded)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Recommendation Not Found",
                Detail = "The specified recommendation was not found or belongs to another user.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(new { Succeeded = true, Feedback = request.Feedback.ToString() });
    }
}
