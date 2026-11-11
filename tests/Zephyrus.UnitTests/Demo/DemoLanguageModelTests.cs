using Zephyrus.Core.Agents;
using Zephyrus.Core.Enums;
using Zephyrus.Infrastructure.AI.Agents;
using Zephyrus.Infrastructure.Demo;
using Zephyrus.UnitTests.Agents;

namespace Zephyrus.UnitTests.Demo;

/// <summary>
/// Demo mode is only useful if every real agent can parse what the demo model
/// returns, so these run the actual agents against it.
/// </summary>
public class DemoLanguageModelTests
{
    private const string Constitution = "project:\n  name: demo";
    private const string Prompt = "let customers download their invoices as PDF";

    private static readonly DemoLanguageModel Model = new();

    private static FakePromptLoader Loader(string agent) => new($"You are the {agent} Agent for Zephyrus.");

    [Fact]
    public async Task PrdAgent_WhenRunAgainstDemoModel_ShouldTitleThePrdAfterTheFeature()
    {
        var output = await new PrdAgent(Model, Loader("PRD")).RunAsync(new PrdAgentInput
        {
            FeaturePrompt = Prompt,
            ProjectConstitution = Constitution,
            FeatureSlug = "invoices-pdf",
        }, Ct);

        Assert.StartsWith("# PRD: Let customers download their invoices as PDF", output.Markdown);
        Assert.Contains("Demo mode", output.Markdown);
    }

    [Fact]
    public async Task ArchitectAgent_WhenGivenADemoPrd_ShouldCarryTheTitleIntoTheAdr()
    {
        var output = await new ArchitectAgent(Model, Loader("Architect")).RunAsync(new ArchitectAgentInput
        {
            ApprovedPrd = "# PRD: Weekly usage email\n\n## Goals",
            ProjectConstitution = Constitution,
            FeatureSlug = "weekly-usage-email",
        }, Ct);

        Assert.StartsWith("# ADR: Weekly usage email", output.Markdown);
    }

    [Fact]
    public async Task TaskAgent_WhenRunAgainstDemoModel_ShouldParseOneTaskPerLayer()
    {
        var output = await new TaskAgent(Model, Loader("Task")).RunAsync(new TaskAgentInput
        {
            ApprovedPrd = "# PRD: Invoices",
            ApprovedAdr = "# ADR: Invoices",
            ProjectConstitution = Constitution,
            FeatureSlug = "invoices",
        }, Ct);

        Assert.Equal(new[] { AgentType.DB, AgentType.BE, AgentType.FE }, output.Tasks.Select(t => t.AgentType));
        Assert.All(output.Tasks, t => Assert.Contains("Invoices", t.Title));
    }

    [Fact]
    public async Task CodeAgent_WhenRunAgainstDemoModel_ShouldGenerateCodeWithoutRequestingFiles()
    {
        var output = await new CodeAgent(Model, Loader("Code")).RunAsync(new CodeAgentInput
        {
            TaskTitle = "Implement the use case",
            TaskBody = "Body",
            ApprovedAdr = "# ADR: Invoices",
            ProjectConstitution = Constitution,
            FeatureSlug = "invoices",
            BranchName = "feature/invoices/1",
        }, Ct);

        Assert.Equal("generate_code", output.Action);
        Assert.Single(output.Files);
        Assert.Empty(output.RequestedFiles);
    }

    [Fact]
    public async Task QaAgent_WhenRunAgainstDemoModel_ShouldReturnTestsAndAReport()
    {
        var output = await new QaAgent(Model, Loader("QA")).RunAsync(new QaAgentInput
        {
            FeatureSlug = "invoices",
            ApprovedAdr = "# ADR: Invoices",
            ProjectConstitution = Constitution,
            Tasks = new[] { new QaTaskContext { TaskTitle = "Task", PrId = 1, BranchName = "feature/invoices/1" } },
        }, Ct);

        Assert.Single(output.TestFiles);
        Assert.StartsWith("# QA Report", output.ReportMarkdown);
    }

    [Fact]
    public async Task DevOpsAgent_WhenRunAgainstDemoModel_ShouldReturnAWorkflow()
    {
        var output = await new DevOpsAgent(Model, Loader("DevOps")).RunAsync(new DevOpsAgentInput
        {
            FeatureSlug = "invoices",
            ProjectConstitution = Constitution,
            DeploymentTarget = "Railway",
            RepositorySlug = "acme/billing",
        }, Ct);

        Assert.StartsWith("name: Deploy", output.WorkflowYaml);
    }

    [Fact]
    public async Task GenerateAsync_WhenPromptIsVeryLong_ShouldShortenTheTitle()
    {
        var longPrompt = new string('a', 200);

        var markdown = await Model.GenerateAsync("You are the PRD Agent", $"## Feature Prompt\n{longPrompt}", Ct);

        var heading = markdown.Split('\n')[0];
        Assert.EndsWith("…", heading);
        Assert.True(heading.Length < 100);
    }
}
