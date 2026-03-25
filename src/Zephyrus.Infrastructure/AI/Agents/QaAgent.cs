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
    private readonly IPromptLoader _promptLoader;

    public QaAgent(ILanguageModel languageModel, IPromptLoader promptLoader)
    {
        _languageModel = languageModel;
        _promptLoader = promptLoader;
    }

    public async Task<QaAgentOutput> RunAsync(QaAgentInput input, CancellationToken ct = default)
    {
        var systemPrompt = await _promptLoader.LoadAsync("qa", ct);

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

        var json = await _languageModel.GenerateAsync(systemPrompt, userMessage, ct);

        var (testFiles, report) = ParseOutput(json);

        var repoPath = $"docs/qa-report-{input.FeatureSlug}.md";

        return new QaAgentOutput
        {
            TestFiles = testFiles,
            ReportMarkdown = report,
            RepositoryPath = repoPath,
            SystemPrompt = systemPrompt,
            UserMessage = userMessage,
            RawResponse = json
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

        if (!root.TryGetProperty("report", out var reportElement))
            throw new InvalidOperationException("QA report is required.");
        var report = reportElement.GetString()
            ?? throw new InvalidOperationException("QA report is required.");

        return (testFiles, report);
    }
}
