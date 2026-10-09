using FluentValidation;
using LinerNotes.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using LinerNotes.Domain.Scoring;

namespace LinerNotes.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Register MediatR and pipeline behaviors
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // Register FluentValidation validators
        services.AddValidatorsFromAssembly(assembly);
        services.AddSingleton<BaselineAScorer>();

        return services;
    }
}
