using LinerNotes.Application.DTOs.Export;
using LinerNotes.Application.Features.Export.Queries.GetUserDataExport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LinerNotes.Presentation.Controllers;

/// <summary>
/// Endpoints for data sovereignty, GDPR transparency, and complete taste data export.
/// </summary>
[Authorize]
public sealed class ExportController : ApiControllerBase
{
    /// <summary>
    /// Exports all stored data held for the current subscriber as a structured, versioned JSON payload.
    /// </summary>
    [HttpGet("my-data")]
    [ProducesResponseType(typeof(UserDataExportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportMyData(
        [FromQuery] bool download = false,
        CancellationToken cancellationToken = default)
    {
        var export = await Mediator.Send(new GetUserDataExportQuery(CurrentUser.UserId), cancellationToken);

        if (download)
        {
            var fileName = $"linernotes-data-{CurrentUser.UserId}-{DateTime.UtcNow:yyyyMMdd}.json";
            Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");
        }

        return Ok(export);
    }
}
