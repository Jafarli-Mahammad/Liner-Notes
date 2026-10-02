using FluentValidation.Results;
using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Presentation.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public class ApiExceptionFilterAttributeTests
{
    private readonly ILogger<ApiExceptionFilterAttribute> _logger;
    private readonly ApiExceptionFilterAttribute _filter;

    public ApiExceptionFilterAttributeTests()
    {
        _logger = Substitute.For<ILogger<ApiExceptionFilterAttribute>>();
        _filter = new ApiExceptionFilterAttribute(_logger);
    }

    [Fact]
    public void OnException_ValidationException_ReturnsBadRequestValidationProblemDetails()
    {
        var failures = new List<ValidationFailure>
        {
            new("Email", "Invalid email format"),
            new("Password", "Password too short")
        };
        var exception = new ValidationException(failures);
        var context = CreateExceptionContext(exception);

        _filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<BadRequestObjectResult>(context.Result);
        var details = Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, details.Status);
        Assert.True(details.Errors.ContainsKey("Email"));
        Assert.True(details.Errors.ContainsKey("Password"));
    }

    [Fact]
    public void OnException_NotFoundException_ReturnsNotFoundProblemDetails()
    {
        var exception = new NotFoundException("Subscriber", "sub-123");
        var context = CreateExceptionContext(exception);

        _filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<NotFoundObjectResult>(context.Result);
        var details = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status404NotFound, details.Status);
        Assert.Contains("sub-123", details.Detail);
    }

    [Fact]
    public void OnException_UnauthorizedAccessException_ReturnsUnauthorizedProblemDetails()
    {
        var exception = new UnauthorizedAccessException("Access denied.");
        var context = CreateExceptionContext(exception);

        _filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        var details = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, details.Status);
    }

    [Fact]
    public void OnException_InvalidOperationException_ReturnsBadRequestProblemDetails()
    {
        var exception = new InvalidOperationException("User already exists.");
        var context = CreateExceptionContext(exception);

        _filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<BadRequestObjectResult>(context.Result);
        var details = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, details.Status);
        Assert.Equal("User already exists.", details.Detail);
    }

    [Fact]
    public void OnException_UnhandledException_ReturnsInternalServerErrorProblemDetails()
    {
        var exception = new Exception("Database connection exploded");
        var context = CreateExceptionContext(exception);

        _filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        var details = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status500InternalServerError, details.Status);
        // Ensure sensitive internal exception message is not leaked
        Assert.DoesNotContain("exploded", details.Detail);
    }

    private static ExceptionContext CreateExceptionContext(Exception exception)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());

        return new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = exception
        };
    }
}
