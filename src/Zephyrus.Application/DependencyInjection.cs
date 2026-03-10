using Microsoft.Extensions.DependencyInjection;
using Zephyrus.Application.Managers;
using Zephyrus.Application.UseCases;

namespace Zephyrus.Application;

/// <summary>
/// Registers Application layer services into the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Managers
        services.AddScoped<ProjectManager>();
        services.AddScoped<FeatureManager>();

        // Use cases (orchestration-heavy operations)
        services.AddScoped<InvokePrdAgentUseCase>();
        services.AddScoped<ApproveArtifactUseCase>();

        return services;
    }
}
