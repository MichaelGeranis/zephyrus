using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;
using Zephyrus.Infrastructure.AI;
using Zephyrus.Infrastructure.AI.Agents;
using Zephyrus.Infrastructure.Demo;
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
        services.AddScoped<DatabaseHealthProbe>();

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
        // Jobs are stored in the database so they survive restarts and are retried.
        services.Configure<AgentJobQueueOptions>(configuration.GetSection(AgentJobQueueOptions.SectionName));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AgentJobSignal>();
        services.AddScoped<IAgentJobRepository, AgentJobRepository>();
        services.AddScoped<IModelChoiceRepository, ModelChoiceRepository>();
        services.AddScoped<IJobQueue, DurableJobQueue>();
        services.AddHostedService<AgentJobWorker>();

        // Code host and language model: GitHub and Claude, or local stand-ins
        // in demo mode so the app runs without any API keys.
        var demo = configuration.GetSection(DemoOptions.SectionName).Get<DemoOptions>() ?? new DemoOptions();

        // Usage is collected per scope and attached to each stored agent run.
        services.Configure<ModelPricingOptions>(configuration.GetSection(ModelPricingOptions.SectionName));
        services.AddScoped<LanguageModelUsageTracker>();
        services.AddScoped<LanguageModelRouting>();

        // Models: the configured chains, and each project's own choice per stage.
        services.Configure<LanguageModelOptions>(configuration.GetSection(LanguageModelOptions.SectionName));
        services.Configure<DemoOptions>(configuration.GetSection(DemoOptions.SectionName));
        services.AddSingleton<IModelCatalog, ModelCatalog>();
        services.AddScoped<IProjectModelSelection, ProjectModelSelection>();

        if (demo.Enabled)
        {
            services.AddSingleton<ICodeHostFactory>(new DemoCodeHostFactory(demo.RepositoryRoot));
            services.AddScoped<ILanguageModel, DemoLanguageModel>();
        }
        else
        {
            // GitHub: the Zephyrus GitHub App where installed, else each
            // project's stored token. An App with a bad key stops startup.
            services.Configure<GitHubAppOptions>(configuration.GetSection(GitHubAppOptions.SectionName));
            (configuration.GetSection(GitHubAppOptions.SectionName).Get<GitHubAppOptions>() ?? new GitHubAppOptions())
                .LoadPrivateKeyIfConfigured();
            services.AddSingleton<IGitHubAppApi, OctokitGitHubAppApi>();
            services.AddSingleton<GitHubAppTokenSource>();
            services.AddSingleton<ICodeHostFactory, GitHubCodeHostFactory>();
            AddLanguageModels(services, configuration);
        }

        // Prompt loader. The prompts ship next to the API build (see
        // Zephyrus.Api.csproj); Prompts:Directory overrides that location.
        var promptsDirectory = configuration["Prompts:Directory"];
        if (string.IsNullOrWhiteSpace(promptsDirectory))
            promptsDirectory = Path.Combine(AppContext.BaseDirectory, "prompts");
        services.AddSingleton<IPromptLoader>(new FilePromptLoader(Path.GetFullPath(promptsDirectory)));

        // Agents — each gets its pipeline stage's model chain.
        services.AddScoped<IAgent<PrdAgentInput, PrdAgentOutput>>(sp =>
            new PrdAgent(StageModel(sp, AgentJobKind.Prd), sp.GetRequiredService<IPromptLoader>()));
        services.AddScoped<IAgent<ArchitectAgentInput, ArchitectAgentOutput>>(sp =>
            new ArchitectAgent(StageModel(sp, AgentJobKind.Architect), sp.GetRequiredService<IPromptLoader>()));
        services.AddScoped<IAgent<TaskAgentInput, TaskAgentOutput>>(sp =>
            new TaskAgent(StageModel(sp, AgentJobKind.Task), sp.GetRequiredService<IPromptLoader>()));
        services.AddScoped<IAgent<CodeAgentInput, CodeAgentOutput>>(sp =>
            new CodeAgent(StageModel(sp, AgentJobKind.Code), sp.GetRequiredService<IPromptLoader>()));
        services.AddScoped<IAgent<QaAgentInput, QaAgentOutput>>(sp =>
            new QaAgent(StageModel(sp, AgentJobKind.Qa), sp.GetRequiredService<IPromptLoader>()));
        services.AddScoped<IAgent<DevOpsAgentInput, DevOpsAgentOutput>>(sp =>
            new DevOpsAgent(StageModel(sp, AgentJobKind.DevOps), sp.GetRequiredService<IPromptLoader>()));

        return services;
    }

    /// <summary>
    /// Model chains are configured under <c>LanguageModel</c>: a default and
    /// optional per-stage overrides. See <see cref="LanguageModelOptions"/>.
    /// </summary>
    private static void AddLanguageModels(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ClaudeLanguageModelOptions>(configuration.GetSection(ClaudeLanguageModelOptions.SectionName));
        services.Configure<LocalModelOptions>(configuration.GetSection(LocalModelOptions.SectionName));
        services.AddHttpClient(LanguageModelChainFactory.ClaudeHttpClient);
        services.AddHttpClient(LanguageModelChainFactory.LocalHttpClient);

        // Fail at startup on a misspelt stage or model reference.
        (configuration.GetSection(LanguageModelOptions.SectionName).Get<LanguageModelOptions>()
            ?? new LanguageModelOptions()).Validate();

        services.AddScoped<LanguageModelChainFactory>();
        services.AddScoped<ILanguageModel>(sp => sp.GetRequiredService<LanguageModelChainFactory>().ForDefault());
    }

    /// <summary>
    /// The model a stage's agent uses: the project's choice for the stage if it
    /// made one, else the stage's configured chain, else the shared
    /// <see cref="ILanguageModel"/> (the default chain, the demo model, or a
    /// test fake). The demo model answers every stage, whatever was chosen.
    /// </summary>
    private static ILanguageModel StageModel(IServiceProvider sp, AgentJobKind stage)
    {
        var chains = sp.GetService<LanguageModelChainFactory>();
        if (chains is null)
            return sp.GetRequiredService<ILanguageModel>();

        var configured = chains.HasStageOverride(stage.ToString())
            ? chains.ForStage(stage.ToString())
            : sp.GetRequiredService<ILanguageModel>();
        return new StageLanguageModel(
            stage,
            configured,
            sp.GetRequiredService<IModelCatalog>().DefaultFor(stage),
            sp.GetRequiredService<IProjectModelSelection>(),
            chains.ForChain);
    }
}
