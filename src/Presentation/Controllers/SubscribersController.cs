using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Application.Features.Subscribers.Commands.DeleteUserAccount;
using LinerNotes.Application.Features.Subscribers.Queries.GetSubscriberProfile;
using LinerNotes.Application.Services;
using LinerNotes.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LinerNotes.Presentation.Controllers;

/// <summary>
/// Subscriber account management endpoints.
/// </summary>
[Authorize]
public sealed class SubscribersController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUnitOfWork _unitOfWork;

    public SubscribersController(IAuthService authService, IUnitOfWork unitOfWork)
    {
        _authService = authService;
        _unitOfWork = unitOfWork;
    }
    /// <summary>
    /// Gets the current subscriber's profile and discovery settings.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(SubscriberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentProfile(CancellationToken cancellationToken)
    {
        var profile = await Mediator.Send(new GetSubscriberProfileQuery(CurrentUser.UserId), cancellationToken);
        if (profile is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Profile Not Found",
                Detail = "Subscriber profile was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(profile);
    }

    /// <summary>
    /// Physically deletes account-owned records and credentials in one transaction.
    /// </summary>
    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInTransactionAsync<IActionResult>(async ct =>
        {
        var succeeded = await Mediator.Send(new DeleteUserAccountCommand(CurrentUser.UserId), cancellationToken);
        if (!succeeded)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Account Not Found",
                Detail = "Subscriber account does not exist or has already been deleted.",
                Status = StatusCodes.Status404NotFound
            });
        }

        // Irreversibly delete Identity credentials alongside domain profile
        if (!await _authService.DeleteUserAsync(CurrentUser.UserId))
            throw new InvalidOperationException("Account credentials could not be deleted.");

        return NoContent();
        }, cancellationToken);
    }
}
