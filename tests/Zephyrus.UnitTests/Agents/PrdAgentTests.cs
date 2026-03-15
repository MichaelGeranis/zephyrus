using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.AI.Agents;

namespace Zephyrus.UnitTests.Agents;

public class PrdAgentTests
{
    private const string SystemPrompt = "You are the PRD Agent";
    private const string PrdMarkdown = "# PRD: Test Feature\n\n## Problem Statement\nTest.";

    private static (PrdAgent Agent, FakeLanguageModel LLM, FakePromptLoader Loader) CreateAgent()
    {
        var llm = new FakeLanguageModel(PrdMarkdown);
        var loader = new FakePromptLoader(SystemPrompt);
        var agent = new PrdAgent(llm, loader);
        return (agent, llm, loader);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldLoadPrdSystemPrompt()
    {
        var (agent, _, loader) = CreateAgent();
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Equal("prd", loader.LastLoadedName);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassFeaturePromptInUserMessage()
    {
        var (agent, llm, _) = CreateAgent();
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.FeaturePrompt, llm.LastUserMessage);
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
        var input = BuildInput();

        var output = await agent.RunAsync(input);

        Assert.Equal(PrdMarkdown, output.Markdown);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldSetRepositoryPathUsingSlug()
    {
        var (agent, _, _) = CreateAgent();
        var input = BuildInput(featureSlug: "add-user-auth");

        var output = await agent.RunAsync(input);

        Assert.Equal("docs/prd-add-user-auth.md", output.RepositoryPath);
    }

    private static PrdAgentInput BuildInput(string featureSlug = "test-feature") =>
        new()
        {
            FeaturePrompt = "Add user authentication",
            ProjectConstitution = "project:\n  name: test-app",
            FeatureSlug = featureSlug
        };
}

file sealed class FakeLanguageModel : ILanguageModel
{
    private readonly string _response;
    public string LastSystemPrompt { get; private set; } = string.Empty;
    public string LastUserMessage { get; private set; } = string.Empty;

    public FakeLanguageModel(string response) => _response = response;

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        LastSystemPrompt = systemPrompt;
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
