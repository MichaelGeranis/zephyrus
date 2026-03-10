namespace Zephyrus.Core.Agents;

/// <summary>
/// Input for the PRD Agent: the feature prompt and the project constitution.
/// </summary>
public sealed class PrdAgentInput
{
    public required string FeaturePrompt { get; init; }
    public required string ProjectConstitution { get; init; }
    public required string FeatureSlug { get; init; }
}
