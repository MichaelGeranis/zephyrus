using Zephyrus.Core.Enums;

namespace Zephyrus.Core.Agents;

/// <summary>
/// Output from the Task Agent: a list of discrete tasks to be created as GitHub Issues.
/// </summary>
public sealed class TaskAgentOutput
{
    public required string Markdown { get; init; }
    public required IReadOnlyList<TaskDefinition> Tasks { get; init; }
    public required string SystemPrompt { get; init; }
    public required string UserMessage { get; init; }
    public required string RawResponse { get; init; }
}

/// <summary>
/// A single task definition extracted from the agent's output.
/// </summary>
public sealed class TaskDefinition
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required AgentType AgentType { get; init; }
}
