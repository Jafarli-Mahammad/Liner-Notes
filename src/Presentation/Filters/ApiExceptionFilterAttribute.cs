using LinerNotes.Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace LinerNotes.Presentation.Filters;

/// <summary>
/// Global exception filter converting domain, application, and infrastructure exceptions
/// into RFC 7807 compliant ProblemDetails responses.
/// </summary>
public sealed class ApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    private readonly ILogger<ApiExceptionFilterAttribute> _logger;
    private readonly IDictionary<Type, Action<ExceptionContext>> _exceptionHandlers;

    public ApiExceptionFilterAttribute(ILogger<ApiExceptionFilterAttribute> logger)
    {
        _logger = logger;
        _exceptionHandlers = new Dictionary<Type, Action<ExceptionContext>>
        {
            { typeof(ValidationException), HandleValidationException },
            { typeof(NotFoundException), HandleNotFoundException },
            { typeof(UnauthorizedAccessException), HandleUnauthorizedAccessException },
            { typeof(InvalidOperationException), HandleInvalidOperationException },
            { typeof(ArgumentException), HandleArgumentException },
            { typeof(LinerNotes.Application.Common.Interfaces.Discovery.DiscoveryRequestConflictException), HandleDiscoveryConflict },
            { typeof(LinerNotes.Application.Common.Interfaces.Discovery.DiscoveryBusyException), HandleDiscoveryBusy },
            { typeof(Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException), HandleAntiforgeryException },
            { typeof(LinerNotes.Application.Common.Interfaces.Recommendation.GenerationStoppedException), HandleDiscoveryStopped }
        };
    }

    public override void OnException(ExceptionContext context)
    {
        HandleException(context);
        base.OnException(context);
    }

    private void HandleException(ExceptionContext context)
    {
        var databaseFailure = context.Exception is Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException
            ? context.Exception.InnerException : context.Exception;
        if (databaseFailure is System.Data.Common.DbException { IsTransient: true })
        {
            _logger.LogWarning("Database temporarily unavailable after {FailureType}.", databaseFailure.GetType().Name);
            context.HttpContext.Response.Headers.RetryAfter = "5";
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "The service is temporarily unavailable.",
                Detail = "Please try again shortly."
            }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
            context.ExceptionHandled = true;
            return;
        }
        var type = context.Exception.GetType();
        if (_exceptionHandlers.TryGetValue(type, out var handler))
        {
            handler.Invoke(context);
            return;
        }

        if (!context.ModelState.IsValid)
        {
            HandleInvalidModelStateException(context);
            return;
        }

        HandleUnknownException(context);
    }

    private static void HandleAntiforgeryException(ExceptionContext context)
    {
        context.Result = new ObjectResult(new ProblemDetails { Status = 403, Title = "Request verification failed." }) { StatusCode = 403 };
        context.ExceptionHandled = true;
    }

    private static void HandleDiscoveryStopped(ExceptionContext context)
    {
        string title = context.HttpContext.Request.Path.StartsWithSegments("/api/auth")
            ? "Authentication is temporarily unavailable. Please retry." : "Discovery is temporarily unavailable.";
        context.Result = new ObjectResult(new ProblemDetails { Status = 503, Title = title }) { StatusCode = 503 };
        context.ExceptionHandled = true;
    }
    private static void HandleDiscoveryConflict(ExceptionContext context)
    {
        context.Result = new ObjectResult(new ProblemDetails { Status = 409, Title = "Your taste or request changed. Refresh and try again." }) { StatusCode = 409 };
        context.ExceptionHandled = true;
    }
    private static void HandleDiscoveryBusy(ExceptionContext context)
    {
        var error = (LinerNotes.Application.Common.Interfaces.Discovery.DiscoveryBusyException)context.Exception;
        int seconds = (int)Math.Clamp(Math.Ceiling(error.RetryAfter.TotalSeconds), 1, 600);
        context.HttpContext.Response.Headers["Retry-After"] = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        context.Result = new ObjectResult(new ProblemDetails { Status = 429, Title = "Please wait before requesting more music." }) { StatusCode = 429 };
        context.ExceptionHandled = true;
    }

    private void HandleValidationException(ExceptionContext context)
    {
        var exception = (ValidationException)context.Exception;

        var details = new ValidationProblemDetails(
            exception.Errors.ToDictionary(k => k.Key, v => v.Value))
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Detail = exception.Message,
            Instance = context.HttpContext.Request.Path
        };

        context.Result = new BadRequestObjectResult(details);
        context.ExceptionHandled = true;
    }

    private void HandleNotFoundException(ExceptionContext context)
    {
        var exception = (NotFoundException)context.Exception;

        var details = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Title = "The specified resource was not found.",
            Status = StatusCodes.Status404NotFound,
            Detail = exception.Message,
            Instance = context.HttpContext.Request.Path
        };

        context.Result = new NotFoundObjectResult(details);
        context.ExceptionHandled = true;
    }

    private void HandleUnauthorizedAccessException(ExceptionContext context)
    {
        var details = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
            Title = "Unauthorized",
            Status = StatusCodes.Status401Unauthorized,
            Detail = context.Exception.Message,
            Instance = context.HttpContext.Request.Path
        };

        context.Result = new ObjectResult(details)
        {
            StatusCode = StatusCodes.Status401Unauthorized
        };
        context.ExceptionHandled = true;
    }

    private void HandleInvalidOperationException(ExceptionContext context)
    {
        var details = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Invalid operation requested.",
            Status = StatusCodes.Status400BadRequest,
            Detail = context.Exception.Message,
            Instance = context.HttpContext.Request.Path
        };

        context.Result = new BadRequestObjectResult(details);
        context.ExceptionHandled = true;
    }

    private void HandleArgumentException(ExceptionContext context)
    {
        var details = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Invalid argument provided.",
            Status = StatusCodes.Status400BadRequest,
            Detail = context.Exception.Message,
            Instance = context.HttpContext.Request.Path
        };

        context.Result = new BadRequestObjectResult(details);
        context.ExceptionHandled = true;
    }

    private void HandleInvalidModelStateException(ExceptionContext context)
    {
        var details = new ValidationProblemDetails(context.ModelState)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Instance = context.HttpContext.Request.Path
        };

        context.Result = new BadRequestObjectResult(details);
        context.ExceptionHandled = true;
    }

    private void HandleUnknownException(ExceptionContext context)
    {
        var locations = (new System.Diagnostics.StackTrace(context.Exception, false).GetFrames() ?? []).Take(4)
            .Select(frame => $"{frame.GetMethod()?.DeclaringType?.FullName}.{frame.GetMethod()?.Name}");
        _logger.LogError("Unhandled {FailureType} in {CodeLocations}.", context.Exception.GetType().FullName, string.Join(" > ", locations));

        var details = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Title = "An error occurred while processing your request.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "An unexpected internal server error occurred. Please try again later.",
            Instance = context.HttpContext.Request.Path
        };

        context.Result = new ObjectResult(details)
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
        context.ExceptionHandled = true;
    }
}
