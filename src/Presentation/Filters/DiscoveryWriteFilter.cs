using System.Runtime.ExceptionServices;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LinerNotes.Presentation.Filters;

/// <summary>Admission for account mutations that share the discovery storage inventory.</summary>
public sealed class DiscoveryWriteFilter(IDiscoveryWriteCoordinator writes, DiscoveryRuntimeOptions options,
    LinerNotes.Presentation.Services.PasswordVerificationPreparation? passwords = null,
    LinerNotes.Application.Common.Interfaces.IEmailPayloadProtection? emailProtection = null,
    LinerNotes.Application.Common.Interfaces.EmailDeliveryOptions? emailOptions = null,
    Microsoft.AspNetCore.DataProtection.IDataProtectionProvider? keys = null)
    : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Key rotation is a separate managed write; never initialize its key ring inside account admission.
        if (emailOptions?.Enabled == true && !HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            emailProtection?.Prepare();
            if (keys is not null) Microsoft.AspNetCore.DataProtection.DataProtectionCommonExtensions.Protect(keys.CreateProtector("LinerNotes.KeyPreparation"), "prepare");
        }
        if (!options.ManagedWritesEnabled || HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            await next();
            return;
        }

        using var passwordAttempt = passwords is not null && context.Controller is LinerNotes.Presentation.Controllers.AuthController &&
            context.ActionArguments.TryGetValue("request", out var payload) && payload is LinerNotes.Presentation.Models.LoginRequest login
                ? await passwords.PrepareAsync(login.Email, login.Password, context.HttpContext.RequestAborted) : null;
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
