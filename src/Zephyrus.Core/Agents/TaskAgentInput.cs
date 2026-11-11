namespace Zephyrus.Core.Agents;

/// <summary>
/// Input for the Task Agent: the approved ADR, approved PRD, and project constitution.
/// </summary>
public sealed class TaskAgentInput
{
    public required string ApprovedPrd { get; init; }
    public required string ApprovedAdr { get; init; }
    public required string ProjectConstitution { get; init; }
    public required string FeatureSlug { get; init; }
}
