using LinerNotes.Application.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace LinerNotes.Presentation.Controllers;

/// <summary>
/// Abstract base controller exposing shared MediatR dispatcher and CurrentUserService access.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    private ICurrentUserService? _currentUser;

    protected ISender Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected ICurrentUserService CurrentUser =>
        _currentUser ??= HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
}
