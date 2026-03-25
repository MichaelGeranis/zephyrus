using Xunit;
using Zephyrus.Core.Agents;
using Zephyrus.Infrastructure.AI.Agents;

namespace Zephyrus.UnitTests.Agents;

public class CodeAgentTests
{
    private const string SystemPrompt = "You are the Code Agent";

    private static string SecondFile => """
            ,
            {
              "path": "src/Controllers/UsersController.cs",
              "content": "namespace App;\n\npublic class UsersController { }"
            }
        """;

    private static string ValidFilesJson(int count = 1) =>
        $$"""
        {
          "files": [
            {
              "path": "src/Services/UserService.cs",
              "content": "namespace App;\n\npublic class UserService { }"
            }{{(count > 1 ? SecondFile : "")}}
          ]
        }
        """;

    private static (CodeAgent Agent, FakeLanguageModel LLM, FakePromptLoader Loader) CreateAgent(
        string response)
    {
        var llm = new FakeLanguageModel(response);
        var loader = new FakePromptLoader(SystemPrompt);
        var agent = new CodeAgent(llm, loader);
        return (agent, llm, loader);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldLoadCodeSystemPrompt()
    {
        var (agent, _, loader) = CreateAgent(ValidFilesJson());

        await agent.RunAsync(BuildInput());

        Assert.Equal("code", loader.LastLoadedName);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassTaskTitleInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(ValidFilesJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.TaskTitle, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassTaskBodyInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(ValidFilesJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.TaskBody, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassBranchNameInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(ValidFilesJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.BranchName, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassAdrInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(ValidFilesJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.ApprovedAdr, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenValidJson_ShouldParseGeneratedFiles()
    {
        var (agent, _, _) = CreateAgent(ValidFilesJson(2));

        var output = await agent.RunAsync(BuildInput());

        Assert.Equal(2, output.Files.Count);
    }

    [Fact]
    public async Task RunAsync_WhenValidJson_ShouldPreserveFilePaths()
    {
        var (agent, _, _) = CreateAgent(ValidFilesJson(1));

        var output = await agent.RunAsync(BuildInput());

        Assert.Equal("src/Services/UserService.cs", output.Files[0].Path);
    }

    [Fact]
    public async Task RunAsync_WhenValidJson_ShouldPreserveFileContents()
    {
        var (agent, _, _) = CreateAgent(ValidFilesJson(1));

        var output = await agent.RunAsync(BuildInput());

        Assert.Contains("UserService", output.Files[0].Content);
    }

    [Fact]
    public async Task RunAsync_WhenJsonWrappedInCodeFences_ShouldStillParseFiles()
    {
        var json = "```json\n" + ValidFilesJson(1) + "\n```";
        var (agent, _, _) = CreateAgent(json);

        var output = await agent.RunAsync(BuildInput());

        Assert.Single(output.Files);
    }

    [Fact]
    public async Task RunAsync_WhenMissingFilePath_ShouldThrowInvalidOperationException()
    {
        var badJson = """{ "files": [{ "content": "some code" }] }""";
        var (agent, _, _) = CreateAgent(badJson);

        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.RunAsync(BuildInput()));
    }

    private static CodeAgentInput BuildInput() =>
        new()
        {
            TaskTitle = "Implement UserService",
            TaskBody = "Add a service that manages user CRUD operations.",
            ApprovedAdr = "# ADR: Test",
            ProjectConstitution = "project:\n  name: test-app",
            FeatureSlug = "test-feature",
            BranchName = "feature/test-feature/task-1"
        };
}
