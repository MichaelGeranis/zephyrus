using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.AI.Agents;

namespace Zephyrus.UnitTests.Agents;

public class DevOpsAgentTests
{
    private const string SystemPrompt = "You are the DevOps Agent";

    private static string ValidDevOpsJson(string yaml = "name: Deploy") =>
        $$"""{ "workflow_yaml": {{System.Text.Json.JsonSerializer.Serialize(yaml)}} }""";

    private static (DevOpsAgent Agent, FakeLanguageModel LLM, FakePromptLoader Loader) CreateAgent(
        string response)
    {
        var llm = new FakeLanguageModel(response);
        var loader = new FakePromptLoader(SystemPrompt);
        var agent = new DevOpsAgent(llm, loader);
        return (agent, llm, loader);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldLoadDevOpsSystemPrompt()
    {
        var (agent, _, loader) = CreateAgent(ValidDevOpsJson());

        await agent.RunAsync(BuildInput());

        Assert.Equal("devops", loader.LastLoadedName);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassRepositorySlugInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(ValidDevOpsJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.RepositorySlug, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassDeploymentTargetInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(ValidDevOpsJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.DeploymentTarget, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassProjectConstitutionInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(ValidDevOpsJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.ProjectConstitution, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenValidJson_ShouldReturnWorkflowYaml()
    {
        var expectedYaml = "name: Deploy";
        var (agent, _, _) = CreateAgent(ValidDevOpsJson(expectedYaml));

        var output = await agent.RunAsync(BuildInput());

        Assert.Equal(expectedYaml, output.WorkflowYaml);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldSetRepositoryPathToWorkflowFile()
    {
        var (agent, _, _) = CreateAgent(ValidDevOpsJson());

        var output = await agent.RunAsync(BuildInput());

        Assert.Equal(".github/workflows/deploy.yml", output.RepositoryPath);
    }

    [Fact]
    public async Task RunAsync_WhenJsonWrappedInCodeFences_ShouldStillParseOutput()
    {
        var json = "```json\n" + ValidDevOpsJson() + "\n```";
        var (agent, _, _) = CreateAgent(json);

        var output = await agent.RunAsync(BuildInput());

        Assert.NotEmpty(output.WorkflowYaml);
    }

    [Fact]
    public async Task RunAsync_WhenMissingWorkflowYaml_ShouldThrowInvalidOperationException()
    {
        var badJson = """{ "other_field": "value" }""";
        var (agent, _, _) = CreateAgent(badJson);

        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.RunAsync(BuildInput()));
    }

    private static DevOpsAgentInput BuildInput() =>
        new()
        {
            RepositorySlug = "myorg/my-app",
            DeploymentTarget = "Railway",
            ProjectConstitution = "project:\n  name: test-app"
        };
}

file sealed class FakeLanguageModel : ILanguageModel
{
    private readonly string _response;
    public string LastUserMessage { get; private set; } = string.Empty;

    public FakeLanguageModel(string response) => _response = response;

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        LastUserMessage = userMessage;
        return Task.FromResult(_response);
    }
}

file sealed class FakePromptLoader : IPromptLoader
{
    private readonly string _content;
    public string LastLoadedName { get; private set; } = string.Empty;

    public FakePromptLoader(string content) => _content = content;

    public Task<string> LoadAsync(string name, CancellationToken ct = default)
    {
        LastLoadedName = name;
        return Task.FromResult(_content);
    }
}
