using System.Text.Json;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.AI.Agents;

/// <summary>
/// Code Agent — implements a single task as code. Takes a task description,
/// ADR context, and project constitution, then generates the required source files.
/// Supports multi-pass: can request files for context before generating code.
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

        string json;
        string userMessage;

        if (input.ConversationHistory is { Count: > 0 })
        {
            userMessage = input.ConversationHistory[^1].Content;
            json = await _languageModel.GenerateAsync(systemPrompt, input.ConversationHistory, maxTokens: 16384, ct);
        }
        else
        {
            userMessage = BuildInitialUserMessage(input);
            json = await _languageModel.GenerateAsync(systemPrompt, userMessage, maxTokens: 16384, ct);
        }

        return ParseResponse(json, systemPrompt, userMessage);
    }

    private static string BuildInitialUserMessage(CodeAgentInput input)
    {
        var message = $"""
            ## Task
            **Title:** {input.TaskTitle}
            **Branch:** {input.BranchName}

            {input.TaskBody}

            ## Architecture Decision Record
            {input.ApprovedAdr}

            ## Project Constitution
            {input.ProjectConstitution}
            """;

        if (!string.IsNullOrWhiteSpace(input.CodebaseMap))
        {
            message += $"""


                ## Codebase Map
                {input.CodebaseMap}
                """;
        }

        return message;
    }

    private static CodeAgentOutput ParseResponse(string json, string systemPrompt, string userMessage)
    {
        json = StripCodeFences(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var action = root.TryGetProperty("action", out var actionElement)
            ? actionElement.GetString() ?? "generate_code"
            : "generate_code";

        if (action == "request_files")
        {
            var files = new List<string>();
            if (root.TryGetProperty("files", out var filesElement))
            {
                foreach (var file in filesElement.EnumerateArray())
                {
                    var path = file.GetString();
                    if (path is not null)
                        files.Add(path);
                }
            }

            string? reasoning = null;
            if (root.TryGetProperty("reasoning", out var reasoningElement))
                reasoning = reasoningElement.GetString();

            return new CodeAgentOutput
            {
                Action = "request_files",
                RequestedFiles = files,
                Reasoning = reasoning,
                SystemPrompt = systemPrompt,
                UserMessage = userMessage,
                RawResponse = json
            };
        }

        return new CodeAgentOutput
        {
            Action = "generate_code",
            Files = ParseFiles(root),
            SystemPrompt = systemPrompt,
            UserMessage = userMessage,
            RawResponse = json
        };
    }

    private static List<GeneratedFile> ParseFiles(JsonElement root)
    {
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

    private static string StripCodeFences(string json)
    {
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
        return json;
    }
}
