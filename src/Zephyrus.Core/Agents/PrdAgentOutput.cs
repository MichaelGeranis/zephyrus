namespace Zephyrus.Core.Agents;

/// <summary>
/// Output from the PRD Agent: the generated PRD markdown content and the repo path.
/// </summary>
public sealed class PrdAgentOutput
{
    public required string Markdown { get; init; }
    public required string SystemPrompt { get; init; }
    public required string UserMessage { get; init; }
    public required string RawResponse { get; init; }
}
