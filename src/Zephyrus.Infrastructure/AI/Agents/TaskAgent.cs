using System.Text.Json;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.AI.Agents;

/// <summary>
/// Task Agent — breaks down an approved ADR into discrete, assignable tasks.
/// Each task maps to a GitHub Issue and is tagged with the responsible agent type.
/// </summary>
public sealed class TaskAgent : IAgent<TaskAgentInput, TaskAgentOutput>
{
    private readonly ILanguageModel _languageModel;
    private readonly IPromptLoader _promptLoader;

    public TaskAgent(ILanguageModel languageModel, IPromptLoader promptLoader)
    {
        _languageModel = languageModel;
        _promptLoader = promptLoader;
    }

    public async Task<TaskAgentOutput> RunAsync(TaskAgentInput input, CancellationToken ct = default)
    {
        var systemPrompt = await _promptLoader.LoadAsync("task", ct);

        var userMessage = $"""
            ## Approved PRD
            {input.ApprovedPrd}

            ## Approved ADR
            {input.ApprovedAdr}

            ## Project Constitution
            {input.ProjectConstitution}
            """;

        var json = await _languageModel.GenerateAsync(systemPrompt, userMessage, ct);

        var tasks = ParseTasks(json);

        var repoPath = $"docs/tasks-{input.FeatureSlug}.md";

        // Build a human-readable markdown summary of the task breakdown
        var markdown = BuildMarkdown(input.FeatureSlug, tasks);

        return new TaskAgentOutput
        {
            Markdown = markdown,
            RepositoryPath = repoPath,
            Tasks = tasks,
            SystemPrompt = systemPrompt,
            UserMessage = userMessage,
            RawResponse = json
        };
    }

    private static List<TaskDefinition> ParseTasks(string json)
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
        var tasksArray = root.GetProperty("tasks");

        var tasks = new List<TaskDefinition>();

        foreach (var taskElement in tasksArray.EnumerateArray())
        {
            var title = taskElement.GetProperty("title").GetString()
                ?? throw new InvalidOperationException("Task title is required.");
            var body = taskElement.GetProperty("body").GetString()
                ?? throw new InvalidOperationException("Task body is required.");
            var agentTypeStr = taskElement.GetProperty("agent_type").GetString()
                ?? throw new InvalidOperationException("Task agent_type is required.");

            if (!Enum.TryParse<AgentType>(agentTypeStr, ignoreCase: true, out var agentType))
            {
                throw new InvalidOperationException(
                    $"Invalid agent_type '{agentTypeStr}'. Must be one of: BE, FE, DB, DevOps.");
            }

            tasks.Add(new TaskDefinition
            {
                Title = title,
                Body = body,
                AgentType = agentType
            });
        }

        return tasks;
    }

    private static string BuildMarkdown(string featureSlug, IReadOnlyList<TaskDefinition> tasks)
    {
        var lines = new List<string>
        {
            $"# Task Breakdown: {featureSlug}",
            "",
            $"Total tasks: {tasks.Count}",
            ""
        };

        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            lines.Add($"## Task {i + 1}: {task.Title}");
            lines.Add($"**Agent:** {task.AgentType}");
            lines.Add("");
            lines.Add(task.Body);
            lines.Add("");
        }

        return string.Join("\n", lines);
    }
}
