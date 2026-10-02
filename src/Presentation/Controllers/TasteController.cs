using LinerNotes.Application.DTOs.Taste;
using LinerNotes.Application.Features.Taste.Commands.SeedTasteProfile;
using LinerNotes.Application.Features.Taste.Queries.GetUserTasteSignals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LinerNotes.Presentation.Controllers;

public sealed record SeedTasteProfileRequest(
    IReadOnlyList<SeedTagDto> Tags,
    IReadOnlyList<SeedArtistDto> Artists);

/// <summary>
/// Taste profile seeding and taste signal inspection endpoints.
/// </summary>
[Authorize]
public sealed class TasteController : ApiControllerBase
{
    /// <summary>
    /// Seeds the subscriber's initial taste profile using 3–5 tags and/or artists (hybrid onboarding).
    /// </summary>
    [HttpPost("seed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SeedProfile(
        [FromBody] SeedTasteProfileRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SeedTasteProfileCommand(
            UserId: CurrentUser.UserId,
            Tags: request.Tags ?? Array.Empty<SeedTagDto>(),
            Artists: request.Artists ?? Array.Empty<SeedArtistDto>());

        var signalsAdded = await Mediator.Send(command, cancellationToken);
        return Ok(new { SignalsAdded = signalsAdded });
    }

    /// <summary>
    /// Retrieves all atomic taste signals recorded for the subscriber.
    /// </summary>
    [HttpGet("signals")]
    [ProducesResponseType(typeof(IReadOnlyList<TasteSignalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTasteSignals(CancellationToken cancellationToken)
    {
        var signals = await Mediator.Send(new GetUserTasteSignalsQuery(CurrentUser.UserId), cancellationToken);
        return Ok(signals);
    }
}
