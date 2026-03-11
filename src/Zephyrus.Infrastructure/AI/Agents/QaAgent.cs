using System.Text.Json;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.AI.Agents;

/// <summary>
/// QA Agent — writes tests and validates code changes. Reads the ADR and
/// task context to generate unit and integration tests for each PR.
/// Produces test files and a summary report.
/// </summary>
public sealed class QaAgent : IAgent<QaAgentInput, QaAgentOutput>
{
    private readonly ILanguageModel _languageModel;

    private const string SystemPrompt = @"You are the QA Agent for Zephyrus, an AI-powered software delivery platform.

Your job is to write tests that validate the code changes made for a feature, and produce a test summary report.

## Output Format

You MUST output ONLY valid JSON (no markdown fences, no commentary) with this exact structure:

{
  ""test_files"": [
    {
      ""path"": ""tests/ExampleTests.cs"",
      ""content"": ""using Xunit;\n\npublic class ExampleTests\n{\n    [Fact]\n    public void Example_ShouldWork()\n    {\n        Assert.True(true);\n    }\n}""
    }
  ],
  ""report"": ""# QA Report\n\n## Summary\n...\n\n## Test Coverage\n...\n\n## Results\n...""
}

## Rules

- Generate complete, compilable test files — not snippets or pseudocode.
- Write both unit tests and integration tests where appropriate.
- Use xUnit for .NET tests, Jest for TypeScript tests — matching the project conventions.
- Test file paths must follow the project's test directory structure.
- Each test must have a descriptive name following the pattern: {Method}_When{Condition}_Should{Result}.
- Cover happy paths, edge cases, and error scenarios.
- The report must include: summary, test count, coverage areas, and any risks identified.
- Do not modify production code — only generate test files.
- Output ONLY the JSON. No preamble, no markdown fences, no commentary.";

    public QaAgent(ILanguageModel languageModel)
    {
        _languageModel = languageModel;
    }

    public async Task<QaAgentOutput> RunAsync(QaAgentInput input, CancellationToken ct = default)
    {
        var taskList = string.Join("\n", input.Tasks.Select(
            t => $"- **{t.TaskTitle}** (PR #{t.PrId}, branch: `{t.BranchName}`)"));

        var userMessage = $"""
            ## Feature: {input.FeatureSlug}

            ## Tasks and PRs
            {taskList}

            ## Architecture Decision Record
            {input.ApprovedAdr}

            ## Project Constitution
            {input.ProjectConstitution}
            """;

        var json = await _languageModel.GenerateAsync(SystemPrompt, userMessage, ct);

        var (testFiles, report) = ParseOutput(json);

        var repoPath = $"docs/qa-report-{input.FeatureSlug}.md";

        return new QaAgentOutput
        {
            TestFiles = testFiles,
            ReportMarkdown = report,
            RepositoryPath = repoPath
        };
    }

    private static (List<GeneratedFile> TestFiles, string Report) ParseOutput(string json)
    {
        // Strip markdown code fences if the LLM wraps the output
        json = json.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline >= 0)
                json = json[(firstNewline + 1)..];
            if (json.EndsWith("```"))
                json = json[..^3];
            json = json.Trim();
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var testFiles = new List<GeneratedFile>();
        foreach (var fileElement in root.GetProperty("test_files").EnumerateArray())
        {
            var path = fileElement.GetProperty("path").GetString()
                ?? throw new InvalidOperationException("Test file path is required.");
            var content = fileElement.GetProperty("content").GetString()
                ?? throw new InvalidOperationException("Test file content is required.");

            testFiles.Add(new GeneratedFile
            {
                Path = path,
                Content = content
            });
        }

        var report = root.GetProperty("report").GetString()
            ?? throw new InvalidOperationException("QA report is required.");

        return (testFiles, report);
    }
}
