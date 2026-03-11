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

    private const string SystemPrompt = @"You are the Code Agent for Zephyrus, an AI-powered software delivery platform.

Your job is to implement a single task by generating the required source code files.

## Output Format

You MUST output ONLY valid JSON (no markdown fences, no commentary) with this exact structure:

{
  ""files"": [
    {
      ""path"": ""src/Zephyrus.Core/Entities/Example.cs"",
      ""content"": ""using System;\n\nnamespace Zephyrus.Core.Entities;\n\npublic class Example\n{\n    // ...\n}""
    }
  ]
}

## Rules

- Generate complete, compilable source files — not diffs or patches.
- Follow the project's architecture strictly: Core for entities/interfaces, Application for use cases, Infrastructure for implementations, Api for controllers.
- Respect the project constitution conventions (naming, patterns, style).
- Each file must have the correct namespace matching its directory path.
- Include necessary using statements.
- Follow C# conventions: PascalCase for public members, camelCase with underscore prefix for private fields.
- For Next.js/TypeScript files, follow the project's frontend conventions.
- Do not generate test files — those are handled by the QA Agent.
- Do not generate documentation files — those are handled by other agents.
- Keep each file focused on a single responsibility.
- Output ONLY the JSON. No preamble, no markdown fences, no commentary.";

    public CodeAgent(ILanguageModel languageModel)
    {
        _languageModel = languageModel;
    }

    public async Task<CodeAgentOutput> RunAsync(CodeAgentInput input, CancellationToken ct = default)
    {
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

        var json = await _languageModel.GenerateAsync(SystemPrompt, userMessage, ct);

        var files = ParseFiles(json);

        return new CodeAgentOutput
        {
            Files = files
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
            var path = fileElement.GetProperty("path").GetString()
                ?? throw new InvalidOperationException("File path is required.");
            var content = fileElement.GetProperty("content").GetString()
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
