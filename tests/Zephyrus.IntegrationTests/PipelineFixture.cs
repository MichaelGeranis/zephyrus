using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zephyrus.Application;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.AI.Agents;
using Zephyrus.Infrastructure.Jobs;
using Zephyrus.Infrastructure.Persistence;
using Zephyrus.Infrastructure.Persistence.Repositories;
using Zephyrus.IntegrationTests.Fakes;

namespace Zephyrus.IntegrationTests;

/// <summary>
/// Shared test fixture that wires up the full DI container with
/// SQLite in-memory database and fake external services.
/// </summary>
public sealed class PipelineFixture : IDisposable
{
    public ServiceProvider ServiceProvider { get; }
    public FakeCodeHost CodeHost { get; }
    public FakeLanguageModel LanguageModel { get; }

    private readonly SqliteConnection _connection;

    public PipelineFixture()
    {
        CodeHost = new FakeCodeHost();
        LanguageModel = new FakeLanguageModel();

        // Keep a single connection open for the lifetime of the fixture.
        // SQLite in-memory databases are destroyed when the last connection closes.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();

        services.AddLogging();

        // Database — all scopes share the same open connection
        services.AddDbContext<ZephyrusDbContext>(options =>
            options.UseSqlite(_connection));

        // Repositories (real implementations, backed by SQLite)
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IFeatureRepository, FeatureRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<ITaskItemRepository, TaskItemRepository>();
        services.AddScoped<IPipelineEventRepository, PipelineEventRepository>();
        services.AddScoped<IAgentInvocationRepository, AgentInvocationRepository>();
        services.AddScoped<IDeploymentRepository, DeploymentRepository>();

        // Fakes for external services
        services.AddSingleton<ICodeHostFactory>(new FakeCodeHostFactory(CodeHost));
        services.AddSingleton<ILanguageModel>(LanguageModel);
        services.AddSingleton<IPromptLoader>(new FakePromptLoader());

        // Agents (real implementations wired to fake LLM)
        services.AddScoped<IAgent<PrdAgentInput, PrdAgentOutput>, PrdAgent>();
        services.AddScoped<IAgent<ArchitectAgentInput, ArchitectAgentOutput>, ArchitectAgent>();
        services.AddScoped<IAgent<TaskAgentInput, TaskAgentOutput>, TaskAgent>();
        services.AddScoped<IAgent<CodeAgentInput, CodeAgentOutput>, CodeAgent>();
        services.AddScoped<IAgent<QaAgentInput, QaAgentOutput>, QaAgent>();
        services.AddScoped<IAgent<DevOpsAgentInput, DevOpsAgentOutput>, DevOpsAgent>();

        // Application layer (managers, orchestrator, use cases)
        services.AddApplication();

        // Run queued agent jobs inline so the cascade is deterministic in tests.
        services.AddScoped<IJobQueue, InlineJobQueue>();

        ServiceProvider = services.BuildServiceProvider();

        // Create the schema
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZephyrusDbContext>();
        db.Database.EnsureCreated();
    }

    public IServiceScope CreateScope() => ServiceProvider.CreateScope();

    public void Dispose()
    {
        ServiceProvider.Dispose();
        _connection.Dispose();
    }
}
