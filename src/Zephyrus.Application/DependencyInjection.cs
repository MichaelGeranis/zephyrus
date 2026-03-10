using Microsoft.Extensions.DependencyInjection;
using Zephyrus.Application.UseCases;

namespace Zephyrus.Application;

/// <summary>
/// Registers Application layer services into the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Project use cases
        services.AddScoped<CreateProjectUseCase>();
        services.AddScoped<GetProjectByIdUseCase>();
        services.AddScoped<GetAllProjectsUseCase>();

        // Feature use cases
        services.AddScoped<CreateFeatureUseCase>();
        services.AddScoped<GetFeatureByIdUseCase>();
        services.AddScoped<GetFeaturesByProjectUseCase>();

        // Agent use cases
        services.AddScoped<InvokePrdAgentUseCase>();

        return services;
    }
}
