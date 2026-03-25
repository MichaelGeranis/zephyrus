using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.AI.Agents;

namespace Zephyrus.UnitTests.Agents;

public class ArchitectAgentTests
{
    private const string SystemPrompt = "You are the Architect Agent";
    private const string AdrMarkdown = "# ADR: Test Feature\n\n## Summary\nTest architecture.";

    private static (ArchitectAgent Agent, FakeLanguageModel LLM, FakePromptLoader Loader) CreateAgent()
    {
        var llm = new FakeLanguageModel(AdrMarkdown);
        var loader = new FakePromptLoader(SystemPrompt);
        var agent = new ArchitectAgent(llm, loader);
        return (agent, llm, loader);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldLoadArchitectSystemPrompt()
    {
        var (agent, _, loader) = CreateAgent();

        await agent.RunAsync(BuildInput());

        Assert.Equal("architect", loader.LastLoadedName);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassApprovedPrdInUserMessage()
    {
        var (agent, llm, _) = CreateAgent();
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.ApprovedPrd, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassProjectConstitutionInUserMessage()
    {
        var (agent, llm, _) = CreateAgent();
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.ProjectConstitution, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldReturnLlmOutputAsMarkdown()
    {
        var (agent, _, _) = CreateAgent();

        var output = await agent.RunAsync(BuildInput());

        Assert.Equal(AdrMarkdown, output.Markdown);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldSetRepositoryPathUsingSlug()
    {
        var (agent, _, _) = CreateAgent();
        var input = BuildInput(featureSlug: "add-user-auth");

        var output = await agent.RunAsync(input);

        Assert.Equal("docs/adr-add-user-auth.md", output.RepositoryPath);
    }

    private static ArchitectAgentInput BuildInput(string featureSlug = "test-feature") =>
        new()
        {
            ApprovedPrd = "# PRD: Test Feature",
            ProjectConstitution = "project:\n  name: test-app",
            FeatureSlug = featureSlug
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
