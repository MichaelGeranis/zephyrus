using Xunit;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Enums;
using Zephyrus.Infrastructure.AI.Agents;

namespace Zephyrus.UnitTests.Agents;

public class TaskAgentTests
{
    private const string SystemPrompt = "You are the Task Agent";

    private static string SecondTask => """
            ,
            {
              "title": "Implement service",
              "body": "Add the business logic service.",
              "agent_type": "BE"
            }
        """;

    private static string ValidTaskJson(int count = 2) =>
        $$"""
        {
          "tasks": [
            {
              "title": "Create database migration",
              "body": "Add EF Core migration for the new table.",
              "agent_type": "DB"
            }{{(count > 1 ? SecondTask : "")}}
          ]
        }
        """;

    private static (TaskAgent Agent, FakeLanguageModel LLM, FakePromptLoader Loader) CreateAgent(
        string response)
    {
        var llm = new FakeLanguageModel(response);
        var loader = new FakePromptLoader(SystemPrompt);
        var agent = new TaskAgent(llm, loader);
        return (agent, llm, loader);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldLoadTaskSystemPrompt()
    {
        var (agent, _, loader) = CreateAgent(ValidTaskJson());

        await agent.RunAsync(BuildInput());

        Assert.Equal("task", loader.LastLoadedName);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassApprovedPrdInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(ValidTaskJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.ApprovedPrd, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassApprovedAdrInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(ValidTaskJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.ApprovedAdr, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenValidJson_ShouldParseTasks()
    {
        var (agent, _, _) = CreateAgent(ValidTaskJson(2));

        var output = await agent.RunAsync(BuildInput());

        Assert.Equal(2, output.Tasks.Count);
    }

    [Fact]
    public async Task RunAsync_WhenValidJson_ShouldMapAgentTypeCorrectly()
    {
        var (agent, _, _) = CreateAgent(ValidTaskJson(1));

        var output = await agent.RunAsync(BuildInput());

        Assert.Equal(AgentType.DB, output.Tasks[0].AgentType);
    }

    [Fact]
    public async Task RunAsync_WhenValidJson_ShouldSetRepositoryPathUsingSlug()
    {
        var (agent, _, _) = CreateAgent(ValidTaskJson());
        var input = BuildInput(featureSlug: "add-user-auth");

        var output = await agent.RunAsync(input);

        Assert.Equal("docs/tasks-add-user-auth.md", output.RepositoryPath);
    }

    [Fact]
    public async Task RunAsync_WhenJsonWrappedInCodeFences_ShouldStillParseTasks()
    {
        var json = "```json\n" + ValidTaskJson(1) + "\n```";
        var (agent, _, _) = CreateAgent(json);

        var output = await agent.RunAsync(BuildInput());

        Assert.Single(output.Tasks);
    }

    [Fact]
    public async Task RunAsync_WhenInvalidAgentType_ShouldThrowInvalidOperationException()
    {
        var badJson = """
            {
              "tasks": [
                {
                  "title": "Some task",
                  "body": "Do something.",
                  "agent_type": "InvalidType"
                }
              ]
            }
            """;
        var (agent, _, _) = CreateAgent(badJson);

        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.RunAsync(BuildInput()));
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldIncludeTaskTitlesInMarkdown()
    {
        var (agent, _, _) = CreateAgent(ValidTaskJson(1));

        var output = await agent.RunAsync(BuildInput());

        Assert.Contains("Create database migration", output.Markdown);
    }

    private static TaskAgentInput BuildInput(string featureSlug = "test-feature") =>
        new()
        {
            ApprovedPrd = "# PRD: Test",
            ApprovedAdr = "# ADR: Test",
            ProjectConstitution = "project:\n  name: test-app",
            FeatureSlug = featureSlug
        };
}
