using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.GitHub;
using Zephyrus.Infrastructure.Persistence;
using Zephyrus.Infrastructure.Persistence.Repositories;

namespace Zephyrus.Infrastructure;

/// <summary>
/// Registers Infrastructure layer services into the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, IConfiguration configuration)
    {
        services.AddDbContext<ZephyrusDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IFeatureRepository, FeatureRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<ITaskItemRepository, TaskItemRepository>();
        services.AddScoped<IPipelineEventRepository, PipelineEventRepository>();

        services.Configure<GitHubCodeHostOptions>(configuration.GetSection(GitHubCodeHostOptions.SectionName));
        services.AddScoped<ICodeHost, GitHubCodeHost>();

        return services;
    }
}
