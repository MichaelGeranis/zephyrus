using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.IntegrationTests;

public class DemoModeIntegrationTests : IClassFixture<ZephyrusApiFactory>
{
    private readonly ZephyrusApiFactory _factory;

    public DemoModeIntegrationTests(ZephyrusApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SeedDemoData_WhenDatabaseIsEmpty_ShouldCreateOneProjectWithAPrdAwaitingApproval()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var seed = scope.ServiceProvider.GetRequiredService<SeedDemoDataUseCase>();

        var created = await seed.ExecuteAsync(Ct);
        var createdAgain = await seed.ExecuteAsync(Ct);

        Assert.True(created);
        Assert.False(createdAgain);

        var projects = await scope.ServiceProvider.GetRequiredService<IProjectRepository>().GetAllAsync(Ct);
        var project = Assert.Single(projects);
        Assert.Equal(SeedDemoDataUseCase.ProjectName, project.Name);

        var features = await scope.ServiceProvider.GetRequiredService<IFeatureRepository>().GetByProjectIdAsync(project.Id, Ct);
        Assert.Equal(FeatureStatus.PrdPending, features.Single(f => f.Prompt == SeedDemoDataUseCase.FeatureWithPrd).Status);
        Assert.Equal(FeatureStatus.Ideation, features.Single(f => f.Prompt == SeedDemoDataUseCase.FeatureInIdeation).Status);
    }

    [Fact]
    public void Startup_WhenDemoTokenIsConfiguredOutsideDemoMode_ShouldRefuseToStart()
    {
        using var factory = new ZephyrusApiFactory();
        var app = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Team:Members:9:Token", "demo-pm"));

        var error = Record.Exception(() => app.CreateClient());

        Assert.NotNull(error);
        Assert.Contains("demo-", (error.InnerException ?? error).Message);
    }

    [Fact]
    public void Startup_WhenDemoTokenIsConfiguredInDemoMode_ShouldStart()
    {
        using var factory = new ZephyrusApiFactory();
        var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Demo:Enabled", "true");
            builder.UseSetting("Team:Members:9:Token", "demo-pm");
        });

        var client = app.CreateClient();

        Assert.NotNull(client);
    }
}
