using LinerNotes.Application.DTOs.Export;
using LinerNotes.Application.Features.Export.Queries.GetUserDataExport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LinerNotes.Presentation.Controllers;

/// <summary>
/// Authenticated export of reviewed account/taste records with explicit security exclusions.
/// </summary>
[Authorize]
public sealed class ExportController : ApiControllerBase
{
    /// <summary>
    /// Exports the reviewed account/taste inventory as a structured, versioned JSON payload.
    /// </summary>
    [HttpGet("my-data")]
    [ProducesResponseType(typeof(UserDataExportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportMyData(
        [FromQuery] bool download = false,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUser.UserId == Guid.Empty)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Authentication is required to export personal data.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var export = await Mediator.Send(new GetUserDataExportQuery(CurrentUser.UserId), cancellationToken);
        if (export is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = "No subscriber data was found to export.",
                Status = StatusCodes.Status404NotFound
            });
        }

        if (download)
        {
            var fileName = $"linernotes-data-{CurrentUser.UserId}-{DateTime.UtcNow:yyyyMMdd}.json";
            Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");
        }

        return Ok(export);
    }
}
