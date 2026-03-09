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
        services.AddScoped<InvokePrdAgentUseCase>();

        return services;
    }
}
