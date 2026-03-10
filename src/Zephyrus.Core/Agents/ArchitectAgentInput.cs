namespace Zephyrus.Core.Agents;

/// <summary>
/// Input for the Architect Agent: the approved PRD and the project constitution.
/// </summary>
public sealed class ArchitectAgentInput
{
    public required string ApprovedPrd { get; init; }
    public required string ProjectConstitution { get; init; }
    public required string FeatureSlug { get; init; }
}
