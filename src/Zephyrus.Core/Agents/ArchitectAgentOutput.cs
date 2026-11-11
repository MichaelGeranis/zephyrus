namespace Zephyrus.Core.Agents;

/// <summary>
/// Output from the Architect Agent: the generated ADR markdown and the repo path.
/// </summary>
public sealed class ArchitectAgentOutput
{
    public required string Markdown { get; init; }
    public required string SystemPrompt { get; init; }
    public required string UserMessage { get; init; }
    public required string RawResponse { get; init; }
}
