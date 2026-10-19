using System.Runtime.ExceptionServices;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LinerNotes.Presentation.Filters;

/// <summary>Admission for account mutations that share the discovery storage inventory.</summary>
public sealed class DiscoveryWriteFilter(IDiscoveryWriteCoordinator writes, DiscoveryRuntimeOptions options)
    : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!options.RunJobs || HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            await next();
            return;
        }

        await writes.ExecuteAsync(131_072, async _ =>
        {
            var result = await next();
            // MVC captures action exceptions; rethrow before admission can commit partial account writes.
            if (result.Exception is { } exception && !result.ExceptionHandled)
                ExceptionDispatchInfo.Capture(exception).Throw();
            return result;
        }, context.HttpContext.RequestAborted);
    }
}
