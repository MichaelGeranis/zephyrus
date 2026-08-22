using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.AI;
using Zephyrus.Infrastructure.AI.Agents;
using Zephyrus.Infrastructure.GitHub;
using Zephyrus.Infrastructure.Jobs;
using Zephyrus.Infrastructure.Persistence;
using Zephyrus.Infrastructure.Persistence.Repositories;
using Zephyrus.Infrastructure.Security;


namespace Zephyrus.Infrastructure;

/// <summary>
/// Registers Infrastructure layer services into the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, IConfiguration configuration)
    {
        // Secret protection — must be registered before the DbContext, which
        // depends on it to encrypt the project token column.
        var dataProtection = services.AddDataProtection().SetApplicationName("Zephyrus");

        // Key ring location. Without this the keys live under the host user
        // profile, which is lost when a container is replaced — and losing them
        // means stored tokens can no longer be decrypted.
        var keyPath = configuration["DataProtection:KeyPath"];
        if (!string.IsNullOrWhiteSpace(keyPath))
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));

        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        // Database
        services.AddDbContext<ZephyrusDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Repositories
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IFeatureRepository, FeatureRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<ITaskItemRepository, TaskItemRepository>();
        services.AddScoped<IPipelineEventRepository, PipelineEventRepository>();
        services.AddScoped<IAgentInvocationRepository, AgentInvocationRepository>();
        services.AddScoped<IDeploymentRepository, DeploymentRepository>();

        // Job queue — agent work runs on a background worker, never inside the
        // request that triggered it.
        services.AddSingleton<BackgroundJobQueue>();
        services.AddSingleton<IJobQueue>(sp => sp.GetRequiredService<BackgroundJobQueue>());
        services.AddHostedService<AgentJobWorker>();

        // GitHub
        services.AddSingleton<ICodeHostFactory, GitHubCodeHostFactory>();

        // Claude API
        services.Configure<ClaudeLanguageModelOptions>(configuration.GetSection(ClaudeLanguageModelOptions.SectionName));
        services.AddHttpClient<ILanguageModel, ClaudeLanguageModel>();

        // Prompt loader
        var promptsDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "prompts");
        services.AddSingleton<IPromptLoader>(new FilePromptLoader(Path.GetFullPath(promptsDirectory)));

        // Agents
        services.AddScoped<IAgent<PrdAgentInput, PrdAgentOutput>, PrdAgent>();
        services.AddScoped<IAgent<ArchitectAgentInput, ArchitectAgentOutput>, ArchitectAgent>();
        services.AddScoped<IAgent<TaskAgentInput, TaskAgentOutput>, TaskAgent>();
        services.AddScoped<IAgent<CodeAgentInput, CodeAgentOutput>, CodeAgent>();
        services.AddScoped<IAgent<QaAgentInput, QaAgentOutput>, QaAgent>();
        services.AddScoped<IAgent<DevOpsAgentInput, DevOpsAgentOutput>, DevOpsAgent>();

        return services;
    }
}
