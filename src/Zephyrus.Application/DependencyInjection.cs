using Microsoft.Extensions.DependencyInjection;

namespace Zephyrus.Application;

/// <summary>
/// Registers Application layer services into the DI container.
/// Use cases will be registered here as they are implemented.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Use cases will be registered here in future build steps.
        return services;
    }
}
