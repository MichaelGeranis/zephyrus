using Xunit;
using Zephyrus.Core.Agents;
using Zephyrus.Infrastructure.AI.Agents;

namespace Zephyrus.UnitTests.Agents;

public class QaAgentTests
{
    private const string SystemPrompt = "You are the QA Agent";

    private static string SecondTestFile => """
            ,
            {
              "path": "tests/UsersControllerTests.cs",
              "content": "public class UsersControllerTests { }"
            }
        """;

    private static string ValidQaJson(int testFileCount = 1) =>
        $$"""
        {
          "test_files": [
            {
              "path": "tests/UserServiceTests.cs",
              "content": "public class UserServiceTests { }"
            }{{(testFileCount > 1 ? SecondTestFile : "")}}
          ],
          "report": "# QA Report\n\n## Results\n- Passed: 5\n- Failed: 0"
        }
        """;

    private static (QaAgent Agent, FakeLanguageModel LLM, FakePromptLoader Loader) CreateAgent(
        string response)
    {
        var llm = new FakeLanguageModel(response);
        var loader = new FakePromptLoader(SystemPrompt);
        var agent = new QaAgent(llm, loader);
        return (agent, llm, loader);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldLoadQaSystemPrompt()
    {
        var (agent, _, loader) = CreateAgent(ValidQaJson());

        await agent.RunAsync(BuildInput());

        Assert.Equal("qa", loader.LastLoadedName);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassFeatureSlugInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(ValidQaJson());
        var input = BuildInput(featureSlug: "add-user-auth");

        await agent.RunAsync(input);

        Assert.Contains("add-user-auth", llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassApprovedAdrInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(ValidQaJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.ApprovedAdr, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldPassTaskDetailsInUserMessage()
    {
        var (agent, llm, _) = CreateAgent(ValidQaJson());
        var input = BuildInput();

        await agent.RunAsync(input);

        Assert.Contains(input.Tasks[0].TaskTitle, llm.LastUserMessage);
        Assert.Contains($"PR #{input.Tasks[0].PrId}", llm.LastUserMessage);
        Assert.Contains(input.Tasks[0].BranchName, llm.LastUserMessage);
    }

    [Fact]
    public async Task RunAsync_WhenValidJson_ShouldParseTestFiles()
    {
        var (agent, _, _) = CreateAgent(ValidQaJson(2));

        var output = await agent.RunAsync(BuildInput());

        Assert.Equal(2, output.TestFiles.Count);
    }

    [Fact]
    public async Task RunAsync_WhenValidJson_ShouldPreserveTestFilePaths()
    {
        var (agent, _, _) = CreateAgent(ValidQaJson(1));

        var output = await agent.RunAsync(BuildInput());

        Assert.Equal("tests/UserServiceTests.cs", output.TestFiles[0].Path);
    }

    [Fact]
    public async Task RunAsync_WhenValidJson_ShouldParseReport()
    {
        var (agent, _, _) = CreateAgent(ValidQaJson());

        var output = await agent.RunAsync(BuildInput());

        Assert.Contains("QA Report", output.ReportMarkdown);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_ShouldSetRepositoryPathUsingSlug()
    {
        var (agent, _, _) = CreateAgent(ValidQaJson());
        var input = BuildInput(featureSlug: "add-user-auth");

        var output = await agent.RunAsync(input);

        Assert.Equal("docs/qa-report-add-user-auth.md", output.RepositoryPath);
    }

    [Fact]
    public async Task RunAsync_WhenJsonWrappedInCodeFences_ShouldStillParseOutput()
    {
        var json = "```json\n" + ValidQaJson(1) + "\n```";
        var (agent, _, _) = CreateAgent(json);

        var output = await agent.RunAsync(BuildInput());

        Assert.Single(output.TestFiles);
    }

    [Fact]
    public async Task RunAsync_WhenMissingReport_ShouldThrowInvalidOperationException()
    {
        var badJson = """{ "test_files": [] }""";
        var (agent, _, _) = CreateAgent(badJson);

        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.RunAsync(BuildInput()));
    }

    private static QaAgentInput BuildInput(string featureSlug = "test-feature") =>
        new()
        {
            FeatureSlug = featureSlug,
            ApprovedAdr = "# ADR: Test",
            ProjectConstitution = "project:\n  name: test-app",
            Tasks =
            [
                new QaTaskContext
                {
                    TaskTitle = "Implement UserService",
                    PrId = 42,
                    BranchName = "feature/test-feature/task-1"
                }
            ]
        };
}
