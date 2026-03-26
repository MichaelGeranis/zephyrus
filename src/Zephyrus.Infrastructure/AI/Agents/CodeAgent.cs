using System.Text.Json;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.AI.Agents;

/// <summary>
/// Code Agent — implements a single task as code. Takes a task description,
/// ADR context, and project constitution, then generates the required source files.
/// One invocation per task; parallelizable where dependencies allow.
/// </summary>
public sealed class CodeAgent : IAgent<CodeAgentInput, CodeAgentOutput>
{
    private readonly ILanguageModel _languageModel;
    private readonly IPromptLoader _promptLoader;

    public CodeAgent(ILanguageModel languageModel, IPromptLoader promptLoader)
    {
        _languageModel = languageModel;
        _promptLoader = promptLoader;
    }

    public async Task<CodeAgentOutput> RunAsync(CodeAgentInput input, CancellationToken ct = default)
    {
        var systemPrompt = await _promptLoader.LoadAsync("code", ct);

        var userMessage = $"""
            ## Task
            **Title:** {input.TaskTitle}
            **Branch:** {input.BranchName}

            {input.TaskBody}

            ## Architecture Decision Record
            {input.ApprovedAdr}

            ## Project Constitution
            {input.ProjectConstitution}
            """;

        var json = await _languageModel.GenerateAsync(systemPrompt, userMessage, maxTokens: 16384, ct);

        var files = ParseFiles(json);

        return new CodeAgentOutput
        {
            Files = files,
            SystemPrompt = systemPrompt,
            UserMessage = userMessage,
            RawResponse = json
        };
    }

    private static List<GeneratedFile> ParseFiles(string json)
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
        var filesArray = root.GetProperty("files");

        var files = new List<GeneratedFile>();

        foreach (var fileElement in filesArray.EnumerateArray())
        {
            if (!fileElement.TryGetProperty("path", out var pathElement))
                throw new InvalidOperationException("File path is required.");
            var path = pathElement.GetString()
                ?? throw new InvalidOperationException("File path is required.");

            if (!fileElement.TryGetProperty("content", out var contentElement))
                throw new InvalidOperationException("File content is required.");
            var content = contentElement.GetString()
                ?? throw new InvalidOperationException("File content is required.");

            files.Add(new GeneratedFile
            {
                Path = path,
                Content = content
            });
        }

        return files;
    }
}
