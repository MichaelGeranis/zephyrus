using Microsoft.Extensions.DependencyInjection;
using Zephyrus.Application.Managers;
using Zephyrus.Application.Orchestration;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Interfaces;

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
        services.AddScoped<ArtifactManager>();

        // Orchestrator
        services.AddScoped<PipelineOrchestrator>();
        services.AddScoped<IAgentJobDispatcher, AgentJobDispatcher>();

        // Use cases (orchestration-heavy operations)
        services.AddScoped<InvokePrdAgentUseCase>();
        services.AddScoped<InvokeArchitectAgentUseCase>();
        services.AddScoped<ApproveArtifactUseCase>();
        services.AddScoped<InvokeTaskAgentUseCase>();
        services.AddScoped<InvokeCodeAgentUseCase>();
        services.AddScoped<InvokeQaAgentUseCase>();
        services.AddScoped<InvokeDevOpsAgentUseCase>();
        services.AddScoped<RetryArtifactCommitUseCase>();
        services.AddScoped<UpdateArtifactContentUseCase>();
        services.AddScoped<RerunStepUseCase>();

        // Code-host event handlers
        services.AddScoped<HandlePullRequestClosedUseCase>();
        services.AddScoped<HandleDeploymentStatusUseCase>();

        return services;
    }
}
