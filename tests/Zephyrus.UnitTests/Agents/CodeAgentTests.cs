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

    private static string GenerateCodeJson(int count = 1) =>
        $$"""
        {
          "action": "generate_code",
          "files": [
            {
              "path": "src/Services/UserService.cs",
              "content": "namespace App;\n\npublic class UserService { }"
            }{{(count > 1 ? SecondFile : "")}}
          ]
        }
        """;

    private static string RequestFilesJson => """
        {
          "action": "request_files",
          "reasoning": "I need to see the existing entity to follow patterns.",
          "files": [
            "src/Core/Entities/Feature.cs",
            "src/Core/Interfaces/IFeatureRepository.cs"
          ]
        }
        """;

    // Legacy format without action field — should default to generate_code
    private static string LegacyFilesJson => """
        {
          "files": [
            {
              "path": "src/Services/UserService.cs",
              "content": "namespace App;\n\npublic class UserService { }"
            }
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
        var (agent, _, loader) = CreateAgent(GenerateCodeJson());

        await agent.RunAsync(BuildInput());

        Assert.Equal("code", loader.LastLoadedName);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassTaskTitleInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(GenerateCodeJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.TaskTitle, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassTaskBodyInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(GenerateCodeJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.TaskBody, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassBranchNameInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(GenerateCodeJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.BranchName, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassAdrInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(GenerateCodeJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.ApprovedAdr, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenGenerateCode_ShouldParseGeneratedFiles()
    {
        var (agent, _, _) = CreateAgent(GenerateCodeJson(2));

        var output = await agent.RunAsync(BuildInput());

        Assert.Equal("generate_code", output.Action);
        Assert.Equal(2, output.Files.Count);
    }

    [Fact]
    public async Task RunAsync_WhenGenerateCode_ShouldPreserveFilePaths()
    {
        var (agent, _, _) = CreateAgent(GenerateCodeJson(1));

        var output = await agent.RunAsync(BuildInput());

        Assert.Equal("src/Services/UserService.cs", output.Files[0].Path);
    }

    [Fact]
    public async Task RunAsync_WhenGenerateCode_ShouldPreserveFileContents()
    {
        var (agent, _, _) = CreateAgent(GenerateCodeJson(1));

        var output = await agent.RunAsync(BuildInput());

        Assert.Contains("UserService", output.Files[0].Content);
    }

    [Fact]
    public async Task RunAsync_WhenJsonWrappedInCodeFences_ShouldStillParseFiles()
    {
        var json = "```json\n" + GenerateCodeJson(1) + "\n```";
        var (agent, _, _) = CreateAgent(json);

        var output = await agent.RunAsync(BuildInput());

        Assert.Single(output.Files);
    }

    [Fact]
    public async Task RunAsync_WhenMissingFilePath_ShouldThrowInvalidOperationException()
    {
        var badJson = """{ "action": "generate_code", "files": [{ "content": "some code" }] }""";
        var (agent, _, _) = CreateAgent(badJson);

        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.RunAsync(BuildInput()));
    }

    [Fact]
    public async Task RunAsync_WhenRequestFiles_ShouldReturnRequestedPaths()
    {
        var (agent, _, _) = CreateAgent(RequestFilesJson);

        var output = await agent.RunAsync(BuildInput());

        Assert.Equal("request_files", output.Action);
        Assert.Equal(2, output.RequestedFiles.Count);
        Assert.Contains("src/Core/Entities/Feature.cs", output.RequestedFiles);
        Assert.Contains("src/Core/Interfaces/IFeatureRepository.cs", output.RequestedFiles);
    }

    [Fact]
    public async Task RunAsync_WhenRequestFiles_ShouldReturnReasoning()
    {
        var (agent, _, _) = CreateAgent(RequestFilesJson);

        var output = await agent.RunAsync(BuildInput());

        Assert.NotNull(output.Reasoning);
        Assert.Contains("existing entity", output.Reasoning);
    }

    [Fact]
    public async Task RunAsync_WhenRequestFiles_ShouldHaveEmptyFiles()
    {
        var (agent, _, _) = CreateAgent(RequestFilesJson);

        var output = await agent.RunAsync(BuildInput());

        Assert.Empty(output.Files);
    }

    [Fact]
    public async Task RunAsync_WhenLegacyFormatWithoutAction_ShouldDefaultToGenerateCode()
    {
        var (agent, _, _) = CreateAgent(LegacyFilesJson);

        var output = await agent.RunAsync(BuildInput());

        Assert.Equal("generate_code", output.Action);
        Assert.Single(output.Files);
    }

    [Fact]
    public async Task RunAsync_WhenConversationHistoryProvided_ShouldUseMultiTurnApi()
    {
        var (agent, llm, _) = CreateAgent(GenerateCodeJson());
        var input = BuildInputWithHistory();

        await agent.RunAsync(input);

        Assert.NotNull(llm.LastMessages);
        Assert.Equal(3, llm.LastMessages.Count);
    }

    [Fact]
    public async Task RunAsync_WhenCodebaseMapProvided_ShouldIncludeInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(GenerateCodeJson());
        var input = BuildInput() with { CodebaseMap = "# Codebase\nsrc/Core/..." };

        await agent.RunAsync(input);

        Assert.Contains("Codebase Map", llm.LastUserMessage);
        Assert.Contains("src/Core/...", llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenNoCodebaseMap_ShouldNotIncludeCodebaseSection()
    {
        var (agent, llm, _) = CreateAgent(GenerateCodeJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.DoesNotContain("Codebase Map", llm.LastUserMessage);
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

    private static CodeAgentInput BuildInputWithHistory() =>
        new()
        {
            TaskTitle = "Implement UserService",
            TaskBody = "Add a service that manages user CRUD operations.",
            ApprovedAdr = "# ADR: Test",
            ProjectConstitution = "project:\n  name: test-app",
            FeatureSlug = "test-feature",
            BranchName = "feature/test-feature/task-1",
            ConversationHistory = new List<ConversationMessage>
            {
                new("user", "Initial task message"),
                new("assistant", """{"action": "request_files", "files": ["src/Feature.cs"]}"""),
                new("user", "## File: src/Feature.cs\n```\npublic class Feature { }\n```")
            }
        };
}
