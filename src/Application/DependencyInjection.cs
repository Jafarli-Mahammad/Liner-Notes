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
        services.AddScoped<LinerNotes.Application.Features.Discovery.GuestLifecycle>();
        services.AddScoped<LinerNotes.Application.Features.Discovery.SelectDiscoveryTaste>();
        services.AddScoped<LinerNotes.Application.Features.Discovery.SaveTasteOnboarding>();
        services.AddScoped<LinerNotes.Application.Features.Discovery.MaterializeDiscoveryTaste>();
        services.AddScoped<LinerNotes.Application.Features.Discovery.PrepareDiscoveryBatch>();
        services.AddScoped<LinerNotes.Application.Features.Discovery.RunDiscoveryJob>();
        services.AddScoped<LinerNotes.Application.Features.Discovery.MaintainDiscoveryStorage>();
        services.AddScoped<LinerNotes.Application.Features.Discovery.CleanupPublicDiscovery>();
        services.AddScoped<LinerNotes.Application.Features.Catalog.ImportCatalog>();
        services.AddScoped<LinerNotes.Application.Features.Catalog.RunCatalogLookup>();
        services.AddScoped<LinerNotes.Application.Features.Catalog.ICatalogLookupJobs, LinerNotes.Application.Features.Catalog.CatalogLookupJobs>();

        return services;
    }
}
