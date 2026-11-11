namespace Zephyrus.Core.Agents;

/// <summary>
/// Input for the Architect Agent: the approved PRD, project constitution, and optional codebase map.
/// </summary>
public sealed class ArchitectAgentInput
{
    public required string ApprovedPrd { get; init; }
    public required string ProjectConstitution { get; init; }
    public required string FeatureSlug { get; init; }
    public string? CodebaseMap { get; init; }
}
