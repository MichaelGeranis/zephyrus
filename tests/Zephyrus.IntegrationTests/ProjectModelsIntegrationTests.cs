using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;

namespace Zephyrus.IntegrationTests;

public class ProjectModelsIntegrationTests : IClassFixture<ZephyrusApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ZephyrusApiFactory _factory;
    private readonly HttpClient _techLead;

    public ProjectModelsIntegrationTests(ZephyrusApiFactory factory)
    {
        _factory = factory;
        _techLead = factory.CreateClient(); // holds every role, Tech Lead included
    }

    private async Task<Guid> CreateProjectAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            name = "Models " + Guid.NewGuid().ToString("N")[..6],
            description = "d",
            config = "project:\n  name: models",
            repositorySlug = "org/models",
            gitHubToken = "ghp_test",
        }, Ct);
        return (await response.Content.ReadFromJsonAsync<IdDto>(JsonOptions, Ct))!.Id;
    }

    private static async Task<ModelsDto> Read(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ModelsDto>(JsonOptions, Ct))!;

    [Fact]
    public async Task Get_WhenProjectChoseNothing_ShouldListEveryStageWithItsDefault()
    {
        var projectId = await CreateProjectAsync(_techLead);

        var models = await Read(await _techLead.GetAsync($"/api/projects/{projectId}/models", Ct));

        Assert.Equal(new[] { "Prd", "Architect", "Task", "Code", "Qa", "DevOps" }, models.Stages.Select(s => s.Stage));
        Assert.All(models.Stages, s => Assert.False(s.IsProjectChoice));
        Assert.True(models.CanChange);
        Assert.NotEmpty(models.KnownModels);
    }

    [Fact]
    public async Task Put_WhenCallerIsTechLead_ShouldSaveTheChoiceWithTheAuthenticatedIdentity()
    {
        var projectId = await CreateProjectAsync(_techLead);

        var response = await _techLead.PutAsJsonAsync($"/api/projects/{projectId}/models/Code",
            new { primary = "claude:claude-opus-5-5", fallback = "none" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var code = (await Read(response)).Stages.Single(s => s.Stage == "Code");
        Assert.True(code.IsProjectChoice);
        Assert.Equal("claude:claude-opus-5-5", code.Primary);
        Assert.Equal("none", code.Fallback);
        Assert.Equal("pm@test.com", code.ChangedBy); // the all-roles member's identity, from the token
    }

    [Fact]
    public async Task Put_WhenCallerIsNotTechLead_ShouldReturn403AndChangeNothing()
    {
        var projectId = await CreateProjectAsync(_techLead);
        var pm = _factory.CreateClientWithToken(ZephyrusApiFactory.PmOnlyToken);

        var response = await pm.PutAsJsonAsync($"/api/projects/{projectId}/models/Code",
            new { primary = "claude:claude-opus-5-5" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var models = await Read(await pm.GetAsync($"/api/projects/{projectId}/models", Ct));
        Assert.False(models.CanChange);
        Assert.All(models.Stages, s => Assert.False(s.IsProjectChoice));
    }

    [Theory]
    [InlineData("Code", "gpt:4", HttpStatusCode.BadRequest)]
    [InlineData("Deploy", "local:qwen3:32b", HttpStatusCode.BadRequest)]
    public async Task Put_WhenStageOrModelIsInvalid_ShouldReturn400(string stage, string primary, HttpStatusCode expected)
    {
        var projectId = await CreateProjectAsync(_techLead);

        var response = await _techLead.PutAsJsonAsync($"/api/projects/{projectId}/models/{stage}", new { primary }, Ct);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldReturnTheStageToItsDefault()
    {
        var projectId = await CreateProjectAsync(_techLead);
        await _techLead.PutAsJsonAsync($"/api/projects/{projectId}/models/Qa", new { primary = "local:qwen3:32b" }, Ct);

        var models = await Read(await _techLead.DeleteAsync($"/api/projects/{projectId}/models/Qa", Ct));

        Assert.False(models.Stages.Single(s => s.Stage == "Qa").IsProjectChoice);
    }

    [Fact]
    public async Task AgentJob_WhenProjectChoseModels_ShouldSelectThemBeforeTheAgentRuns()
    {
        var selection = new RecordingSelection();
        using var server = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IProjectModelSelection>(_ => selection)));
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", ZephyrusApiFactory.AllRolesToken);

        var projectId = await CreateProjectAsync(client);
        await client.PutAsJsonAsync($"/api/projects/{projectId}/models/Prd", new { primary = "local:qwen3:32b", fallback = "none" }, Ct);
        var feature = await (await client.PostAsJsonAsync("/api/features", new { projectId, prompt = "Pick models" }, Ct))
            .Content.ReadFromJsonAsync<IdDto>(JsonOptions, Ct);

        var response = await client.PostAsync($"/api/features/{feature!.Id}/generate-prd", null, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(new ModelChoice("local:qwen3:32b", "none"), Assert.Single(selection.Selected)[AgentJobKind.Prd]);
    }

    /// <summary>Records what the dispatcher selected; reports no choice so the fake model still answers.</summary>
    private sealed class RecordingSelection : IProjectModelSelection
    {
        public List<IReadOnlyDictionary<AgentJobKind, ModelChoice>> Selected { get; } = new();

        public void Select(IReadOnlyDictionary<AgentJobKind, ModelChoice> choices) => Selected.Add(choices);

        public ModelChoice? For(AgentJobKind stage) => null;
    }

    private sealed record IdDto(Guid Id);

    private sealed record StageDto(string Stage, string Primary, string? Fallback, bool IsProjectChoice, string? ChangedBy, DateTime? ChangedAt);

    private sealed record ModelsDto(StageDto[] Stages, bool CanChange, string[] KnownModels);
}
